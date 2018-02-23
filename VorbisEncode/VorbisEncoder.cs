using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VorbisEncode
{
    /// <summary>
    /// Allows encoding of raw audio data to the Vorbis codec. Requires libvorbis.dll and libogg.dll
    /// </summary>
    public partial class VorbisEncoder : IDisposable
    {
        public const int DEFAULT_SAMPLES = 44100;
        public const int DEFAULT_CHANNELS = 2;
        public const float DEFAULT_QUALITY = 0.5f;

        // ogg and Vorbis structs used for encoding
        private ogg_stream_state os;
        private ogg_page og;
        private ogg_packet op;
        private vorbis_info vi;
        private vorbis_comment vc;
        private vorbis_block vb;
        private vorbis_dsp_state vd;

        // What the fuck am I doing jesus christ
        // The GC likes to move things around and cause Access Violation Exceptions. We are going to abuse GCHandles to not let that happen
        private GCHandle osh;
        private GCHandle ogh;
        private GCHandle oph;
        private GCHandle vih;
        private GCHandle vch;
        private GCHandle vbh;
        private GCHandle vdh;

        private Random m_rand;
        private bool m_bos; // Beginning of Stream
        private bool m_first; // First encode with this object
        private RingBuffer m_encRB; // Somewhere to store encoded data after PutBytes() is used
        private byte[] m_encBuf; // Byte buffer to retrieve the encoded data from libvorbis

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in VBR mode,
        /// with none of the parameters filled in.
        /// </summary>
        public VorbisEncoder()
        {
            m_rand = new Random();
            Mode = BitrateMode.VBR;
            m_encRB = null;
            m_bos = m_first = true;
            Channels = DEFAULT_CHANNELS;
            SampleRate = DEFAULT_SAMPLES;
            Quality = DEFAULT_QUALITY;

            osh = GCHandle.Alloc(os, GCHandleType.Pinned);
            ogh = GCHandle.Alloc(og, GCHandleType.Pinned);
            oph = GCHandle.Alloc(op, GCHandleType.Pinned);
            vih = GCHandle.Alloc(vi, GCHandleType.Pinned);
            vch = GCHandle.Alloc(vc, GCHandleType.Pinned);
            vbh = GCHandle.Alloc(vb, GCHandleType.Pinned);
            vdh = GCHandle.Alloc(vd, GCHandleType.Pinned);
        }

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in VBR mode.
        /// </summary>
        /// <param name="channels">The number of audio channels. Must be 2 for Stereo, else will default to Mono.</param>
        /// <param name="samplerate">The sample rate to encode in. Must match input sample rate.</param>
        /// <param name="quality">Determines the quality of the encoding. Should be between 0.0f and 1.0f.</param>
        public VorbisEncoder(int channels, int samplerate, float quality) : this()
        {
            Channels = channels;
            SampleRate = samplerate;
            Quality = quality;
        }

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in CBR mode.
        /// </summary>
        /// <param name="channels">The number of audio channels. Must be 2 for Stereo, else will default to Mono.</param>
        /// <param name="samplerate">The sample rate to encode in. Must match input sample rate.</param>
        /// <param name="bitrate">The bitrate to encode at, in kbps (e.g. 192 for 192kbps).</param>
        public VorbisEncoder(int channels, int samplerate, int bitrate) : this()
        {
            Channels = channels;
            SampleRate = samplerate;
            Bitrate = bitrate;
            Mode = BitrateMode.CBR;
        }

        /// <summary>
        /// Finalizer. Why does this need a summary?
        /// </summary>
        ~VorbisEncoder()
        {
            Dispose(false);
        }

        /// <summary>
        /// Current bitrate, if CBR encoding is being used.
        /// </summary>
        public int Bitrate { get; set; }

        /// <summary>
        /// Current quality, if VBR encoding is being used.
        /// </summary>
        public float Quality { get; set; }

        /// <summary>
        /// Current sample rate of the encoding.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Current channel count of the encoding. NOTE: Only mono and stereo supported at this time.
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Current encoding bitrate mode. Default is VBR
        /// </summary>
        public BitrateMode Mode { get; set; }

        /// <summary>
        /// True if we have reached the end of a logical stream. Else false.
        /// </summary>
        public bool EOS { get; private set; }

        /// <summary>
        /// Current MetaData to be used when initializing a vorbis stream. Use <see cref="ChangeMetaData"/> to end the current logical stream and update the metadata.
        /// </summary>
        public Dictionary<string, string> MetaData { get; set; }

        /// <summary>
        /// Initialize all ogg and vorbis structs for encoding with the current settings.
        /// <see cref="Close"/> must be called first if you have already created a stream with this object and want to create another.
        /// <seealso cref="Reinit(Dictionary{string, string})"/>
        /// </summary>
        /// <param name="meta">Metadata to be used for the new stream. <seealso cref="ChangeMetaData(Dictionary{string, string})"/></param>
        /// <returns></returns>
        public int Init(Dictionary<string, string> meta = null)
        {
            int ret = 0;
            EOS = false;
            m_bos = true;
            m_first = false;
            meta = meta ?? MetaData;

            /********** Encode setup ************/

            vorbis_info_init(vih.AddrOfPinnedObject());

            // Function called depends on whether we are doing a VBR or CBR stream
            if (Mode == BitrateMode.CBR)
                ret = vorbis_encode_init(vih.AddrOfPinnedObject(), Channels, SampleRate, Bitrate * 1000, Bitrate * 1000, Bitrate * 1000);
            else if (Mode == BitrateMode.VBR)
                ret = vorbis_encode_init_vbr(vih.AddrOfPinnedObject(), Channels, SampleRate, Quality);

            if (ret != 0) return ret;

            // add comments
            vorbis_comment_init(vch.AddrOfPinnedObject());

            if (meta != null)
            {
                foreach (var tag in meta.Keys)
                {
                    vorbis_comment_add_tag(vch.AddrOfPinnedObject(), tag, meta[tag]);
                }
            }

            // set up the analysis state and auxiliary encoding storage
            vorbis_analysis_init(vdh.AddrOfPinnedObject(), vih.AddrOfPinnedObject());
            vorbis_block_init(vdh.AddrOfPinnedObject(), vbh.AddrOfPinnedObject());

            return 0;
        }

        //This function needs to be called before
        //every connection
        /// <summary>
        /// Initializes and retrieves the ogg header packets from libogg and libvorbis.
        /// Must be called after <see cref="Init(Dictionary{string, string})"/> and before <see cref="Encode(byte[], byte[], int)"/>
        /// </summary>
        public void WriteHeader()
        {
            // Comments from vorbis ecoder example
            /* set up our packet->stream encoder */
            /* pick a random serial number; that way we can more likely build
               chained streams just by concatenation */
            ogg_stream_init(osh.AddrOfPinnedObject(), m_rand.Next());
            ogg_packet header = default(ogg_packet);
            ogg_packet header_comm = default(ogg_packet);
            ogg_packet header_code = default(ogg_packet);

            vorbis_analysis_headerout(vdh.AddrOfPinnedObject(), vch.AddrOfPinnedObject(), ref header, ref header_comm, ref header_code);
            ogg_stream_packetin(osh.AddrOfPinnedObject(), ref header); /* automatically placed in its own page */
            ogg_stream_packetin(osh.AddrOfPinnedObject(), ref header_comm);
            ogg_stream_packetin(osh.AddrOfPinnedObject(), ref header_code);
        }

        /// <summary>
        /// Calls <see cref="Close"/> and then <see cref="Init(Dictionary{string, string})"/>
        /// Can be used instead of vorbis_enc_init always.
        /// </summary>
        /// <param name="meta">Metadata to be used for the new stream. <seealso cref="ChangeMetaData(Dictionary{string, string})"/></param>
        /// <returns></returns>
        public int Reinit(Dictionary<string, string> meta = null)
        {
            Close();
            return Init(meta);
        }

        /// <summary>
        /// Encodes the raw audio bytes fed to it to Vorbis.
        /// </summary>
        /// <param name="pcmBuffer">Array containing the raw audio data</param>
        /// <param name="encBuffer">Array to place the encoded audio data into</param>
        /// <param name="size">How many bytes in the raw audio buffer to encode</param>
        /// <returns></returns>
        public int Encode(byte[] pcmBuffer, byte[] encBuffer, int size)
        {
            int result;
            int encoded_bytes = 0;

            /* This ensures the actual
             * audio data will start on a new page, as per spec
             */
            // Not EOS and is beginning of stream. Ensures we don't go through this block more than just the first call to this function for each new stream
            while (!EOS && m_bos)
            {
                result = ogg_stream_flush(osh.AddrOfPinnedObject(), ogh.AddrOfPinnedObject());

                // The original object the GCHandle was created for does not seem to update when the GCHandle address for it is used to manipulate the object
                og = (ogg_page)ogh.Target;

                if (result == 0)
                {
                    m_bos = false;
                    break;
                }

                Marshal.Copy(og.header, encBuffer, encoded_bytes, og.header_len);
                encoded_bytes += og.header_len;
                Marshal.Copy(og.body, encBuffer, encoded_bytes, og.body_len);
                encoded_bytes += og.body_len;
            }


            if (size == 0)
            {
                // Let libvorbis prepare for end of stream
                vorbis_analysis_wrote(vdh.AddrOfPinnedObject(), 0);
            }
            else
            {
                int i;

                unsafe
                {
                    // Get a buffer from libvorbis as a double float poiner
                    float** vab = (float**)(vorbis_analysis_buffer(vdh.AddrOfPinnedObject(), size).ToPointer());

                    //deinterlace audio data and convert it from short to float
                    // We are dealing with bytes but audio samples are 16-bits, so convert each pair of bytes to a short, then to a float
                    if (Channels == 2) // stereo
                    {
                        for (i = 0; i < size / 4; i++)
                        {
                            vab[0][i] = (BitConverter.ToInt16(pcmBuffer, i * 4) / 32768f);
                            vab[1][i] = (BitConverter.ToInt16(pcmBuffer, i * 4 + 2) / 32768f);
                        }
                    }
                    else // mono
                    {
                        for (i = 0; i < size / 2; i++)
                        {
                            vab[0][i] = (BitConverter.ToInt16(pcmBuffer, i * 2) / 32768f);
                        }
                    }
                }

                // Tell libvorbis how much data we actually wrote to the buffer
                vorbis_analysis_wrote(vdh.AddrOfPinnedObject(), i);
            }

            while (vorbis_analysis_blockout(vdh.AddrOfPinnedObject(), vbh.AddrOfPinnedObject()) == 1)
            {
                // When using a managed bitrate mode, a null pointer should be passed to vorbis_analysis
                if (Mode == BitrateMode.CBR)
                    vorbis_analysis(vbh.AddrOfPinnedObject(), IntPtr.Zero);
                else
                    vorbis_analysis(vbh.AddrOfPinnedObject(), oph.AddrOfPinnedObject());

                vorbis_bitrate_addblock(vbh.AddrOfPinnedObject());

                while (vorbis_bitrate_flushpacket(vdh.AddrOfPinnedObject(), oph.AddrOfPinnedObject()) != 0)
                {
                    /* weld the packet into the bitstream */
                    ogg_stream_packetin(osh.AddrOfPinnedObject(), oph.AddrOfPinnedObject());

                    /* write out pages (if any) */
                    while (!EOS)
                    {
                        result = ogg_stream_pageout(osh.AddrOfPinnedObject(), ogh.AddrOfPinnedObject());
                        if (result == 0)
                            break;

                        og = (ogg_page)ogh.Target;

                        Marshal.Copy(og.header, encBuffer, encoded_bytes, og.header_len);
                        encoded_bytes += og.header_len;
                        Marshal.Copy(og.body, encBuffer, encoded_bytes, og.body_len);
                        encoded_bytes += og.body_len;

                        if (ogg_page_eos(ogh.AddrOfPinnedObject()) != 0)
                        {
                            EOS = true;
                        }
                    }
                }
            }

            return encoded_bytes;
        }

        /// <summary>
        /// Call all the *_*_clear() methods of libvorbis and libogg to proper clear all structs and memory.
        /// </summary>
        public void Close()
        {
            // clean up. vorbis_info_clear() must be called last
            ogg_stream_clear(osh.AddrOfPinnedObject());
            vorbis_block_clear(vbh.AddrOfPinnedObject());
            vorbis_dsp_clear(vdh.AddrOfPinnedObject());
            vorbis_comment_clear(vch.AddrOfPinnedObject());
            vorbis_info_clear(vih.AddrOfPinnedObject());
        }

        /// <summary>
        /// Encodes all data from stdin to stdout. Probably best used with FileStreams.
        /// </summary>
        /// <param name="stdin">The stream to read raw audio data from.</param>
        /// <param name="stdout">The stream to write encoded audio data to.</param>
        public void EncodeStream(Stream stdin, Stream stdout)
        {
            // Create our buffers for raw and encoded data
            var audio_buf = new byte[SampleRate * 10];
            m_encBuf = new byte[SampleRate * 10];
            int bytes_read, enc_bytes_read;

            // Initialize the structs
            Reinit(MetaData);
            WriteHeader();

            // Read raw, encode, and write encoded data until nothing is left in the input stream
            while (!EOS)
            {
                bytes_read = stdin.Read(audio_buf, 0, audio_buf.Length);
                enc_bytes_read = Encode(audio_buf, m_encBuf, bytes_read);

                stdout.Write(m_encBuf, 0, enc_bytes_read);
                stdout.Flush();
            }
        }

        /// <summary>
        /// Async version of <see cref="EncodeStream(Stream, Stream)"/>
        /// Encodes all data from stdin to stdout asynchronously. Probably best used with FileStreams.
        /// </summary>
        /// <param name="stdin">The stream to read raw audio data from.</param>
        /// <param name="stdout">The stream to write encoded audio data to.</param>
        /// <returns></returns>
        public Task EncodeStreamAsync(Stream stdin, Stream stdout)
        {
            return Task.Run(() => EncodeStream(stdin, stdout));
        }

        /// <summary>
        /// Feed raw audio data to the encoder and output them to the internal buffer.
        /// To be used with <see cref="GetBytes(byte[], int)"/> for retrieving the encoded data.
        /// </summary>
        /// <param name="audioBuffer">Array containing raw audio data.</param>
        /// <param name="count">How many bytes of raw audio data to encode.</param>
        public void PutBytes(byte[] audioBuffer, int count)
        {
            int enc_bytes_read = 0;

            // Initialize our array
            if (m_encBuf == null)
                m_encBuf = new byte[SampleRate * 10];

            // Initialize our internal buffer in the form of a MemoryStream, and set the renew threshold
            if (m_encRB == null)
            {
                m_encRB = new RingBuffer(count * Channels * 10);
            }

            // If beginning of stream, initialize the ogg/vorbis structs
            // If not the first stream encoded with this object, make sure the previous stream ended properly
            if (m_bos)
            {
                if (!m_first && !EOS)
                {
                    enc_bytes_read = Encode(audioBuffer, m_encBuf, 0);
                    m_encRB.Write(m_encBuf, 0, enc_bytes_read);
                }

                Reinit(MetaData);
                WriteHeader();
            }

            // Encode data and write to our internal buffer
            enc_bytes_read = Encode(audioBuffer, m_encBuf, count);
            m_encRB.Write(m_encBuf, 0, enc_bytes_read);
        }

        /// <summary>
        /// Retrieve encoded audio data from the internal buffer.
        /// To be used with <see cref="PutBytes(byte[], int)"/> to encode raw data.
        /// </summary>
        /// <param name="buffer">Array to place encoded data into.</param>
        /// <param name="count">How many bytes of data to read from the buffer.</param>
        /// <returns></returns>
        public int GetBytes(byte[] buffer, int count)
        {
            int bytesRead = 0;

            // No sense trying to read from a null stream
            if (m_encRB != null)
                bytesRead = m_encRB.Read(buffer, 0, count);

            return bytesRead;
        }

        /// <summary>
        /// Updates <see cref="MetaData"/> and sets a flag to end the current logical stream and start a new one.
        /// </summary>
        /// <param name="meta">The metadata to be set</param>
        public void ChangeMetaData(Dictionary<string, string> meta)
        {
            if (meta != null)
            {
                MetaData = meta;
                m_bos = true;
            }
        }

        /// <summary>
        /// Create the internal buffer with the specified size.
        /// Only useful when using <see cref="PutBytes(byte[], int)"/> and <see cref="GetBytes(byte[], int)"/>.
        /// Must be called before <see cref="PutBytes(byte[], int)"/> for the first time, else it will have no effect, because fuck you I don't know how to code.
        /// Default size if this is not called is 2x the count passed to <see cref="PutBytes(byte[], int)"/>
        /// </summary>
        /// <param name="size">The size of the buffer. Cannot be changed.</param>
        public void SetInternalBufferSize(long size)
        {
            if (m_encRB == null)
                m_encRB = new RingBuffer(size);
        }

        /// <summary>
        /// Fully dispose of the ogg/vorbis structs and all GCHandles.
        /// Obviously, you cannot continue to use this instance after this is called.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            Close();
            osh.Free();
            ogh.Free();
            oph.Free();
            vih.Free();
            vch.Free();
            vbh.Free();
            vdh.Free();
        }

        /// <summary>
        /// Types of bitrate modes for encoding
        /// </summary>
        public enum BitrateMode
        {
            VBR,
            CBR
        };
    }
}
