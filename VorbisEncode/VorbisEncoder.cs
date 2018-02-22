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
        // ogg and Vorbis structs used for encoding
        private ogg_stream_state os; 
        private ogg_page og;
        private ogg_packet op; 
        private vorbis_info vi;
        private vorbis_comment vc; 
        private vorbis_block vb;
        private vorbis_dsp_state vd; 
        
        private Random m_rand;
        private bool m_bos; // Beginning of Stream
        private bool m_first; // First encode with this object
        private Stream m_encStream; // Being used as our buffer when using Put/Get Bytes 
        private byte[] m_encBuf; // Byte buffer to retrieve the encoded data from libvorbis
        private long m_readPos, m_writePos, m_streamThreshold; // Current read and write position, and the read threshold we re-make the stream buffer
        private static object m_locker = new object();

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in VBR mode,
        /// with none of the parameters filled in.
        /// </summary>
        public VorbisEncoder()
        {
            m_rand = new Random();
            Mode = BitrateMode.VBR;
            m_encStream = null;
            m_bos = m_first = true;
            m_readPos = m_writePos = 0;
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

            vorbis_info_init(ref vi);

            // Function called depends on whether we are doing a VBR or CBR stream
            if (Mode == BitrateMode.CBR)
                ret = vorbis_encode_init(ref vi, Channels, SampleRate, Bitrate * 1000, Bitrate * 1000, Bitrate * 1000);
            else if (Mode == BitrateMode.VBR)
                ret = vorbis_encode_init_vbr(ref vi, Channels, SampleRate, Quality);

            if (ret != 0) return ret;

            // add comments
            vorbis_comment_init(ref vc);

            if (meta != null)
            {
                foreach (var tag in meta.Keys)
                {
                    vorbis_comment_add_tag(ref vc, tag, meta[tag]);
                }
            }

            // set up the analysis state and auxiliary encoding storage
            vorbis_analysis_init(ref vd, ref vi);
            vorbis_block_init(ref vd, ref vb);

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
            ogg_stream_init(ref os, m_rand.Next());
            ogg_packet header = default(ogg_packet);
            ogg_packet header_comm = default(ogg_packet);
            ogg_packet header_code = default(ogg_packet);

            vorbis_analysis_headerout(ref vd, ref vc, ref header, ref header_comm, ref header_code);
            ogg_stream_packetin(ref os, ref header); /* automatically placed in its own page */
            ogg_stream_packetin(ref os, ref header_comm);
            ogg_stream_packetin(ref os, ref header_code);
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
                result = ogg_stream_flush(ref os, ref og);

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
                vorbis_analysis_wrote(ref vd, 0);
            }
            else
            {
                int i;

                unsafe
                {
                    // Get a buffer from libvorbis as a double float poiner
                    float** vab = (float**)(vorbis_analysis_buffer(ref vd, size).ToPointer());

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
                vorbis_analysis_wrote(ref vd, i);
            }

            while (vorbis_analysis_blockout(ref vd, ref vb) == 1)
            {
                // When using a managed bitrate mode, a null pointer should be passed to vorbis_analysis
                if (Mode == BitrateMode.CBR)
                    vorbis_analysis(ref vb, IntPtr.Zero);
                else
                    vorbis_analysis(ref vb, ref op);

                vorbis_bitrate_addblock(ref vb);

                while (vorbis_bitrate_flushpacket(ref vd, ref op) != 0)
                {
                    /* weld the packet into the bitstream */
                    ogg_stream_packetin(ref os, ref op);

                    /* write out pages (if any) */
                    while (!EOS)
                    {
                        result = ogg_stream_pageout(ref os, ref og);
                        if (result == 0)
                            break;
                        
                        Marshal.Copy(og.header, encBuffer, encoded_bytes, og.header_len);
                        encoded_bytes += og.header_len;
                        Marshal.Copy(og.body, encBuffer, encoded_bytes, og.body_len);
                        encoded_bytes += og.body_len;

                        if (ogg_page_eos(ref og) != 0)
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
            ogg_stream_clear(ref os);
            vorbis_block_clear(ref vb);
            vorbis_dsp_clear(ref vd);
            vorbis_comment_clear(ref vc);
            vorbis_info_clear(ref vi);
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
            if (m_encStream == null)
            {
                m_encStream = new MemoryStream();
                m_streamThreshold = SampleRate * Channels * 10;
            }

            // If beginning of stream, initialize the ogg/vorbis structs
            // If not the first stream encoded with this object, make sure the previous stream ended properly
            if (m_bos)
            {
                if (!m_first && !EOS)
                {
                    enc_bytes_read = Encode(audioBuffer, m_encBuf, 0);
                    WriteToEncStream(m_encBuf, enc_bytes_read);
                }
                
                Reinit(MetaData);
                WriteHeader();
            }

            // Encode data and write to our internal buffer
            enc_bytes_read = Encode(audioBuffer, m_encBuf, count);
            WriteToEncStream(m_encBuf, enc_bytes_read);
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
            if (m_encStream != null)
                bytesRead = ReadFromEncStream(buffer, count);

            return bytesRead;
        }

        private void WriteToEncStream(byte[] enc_buf, int count)
        {
            // Lock all writes to our internal buffer so we don't write while we read
            lock (m_locker)
            {
                m_encStream.Position = m_writePos;
                m_encStream.Write(enc_buf, 0, count);
                m_encStream.Flush();
                m_writePos = m_encStream.Position;
            }
        }

        private int ReadFromEncStream(byte[] buffer, int count)
        {
            int readBytes = 0;

            // Lock all reads from our internal buffer so we don't read while we write
            lock (m_locker)
            {
                m_encStream.Position = m_readPos;
                readBytes = m_encStream.Read(buffer, 0, count);
                m_readPos = m_encStream.Position;

                // If we have read enough bytes, recreate the buffer to prevent it from growing too large
                if (m_readPos >= m_streamThreshold)
                {
                    var newStream = new MemoryStream();
                    m_encStream.CopyTo(newStream);
                    m_readPos = 0;
                    m_writePos = newStream.Position;
                    m_encStream = newStream;
                }
            }

            return readBytes;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            Close();

            if(disposing)
            {
                if (m_encStream != null)
                {
                    m_encStream.Dispose();
                    m_encStream = null;
                }
            }
        }

        public enum BitrateMode
        {
            VBR,
            CBR
        };
    }
}
