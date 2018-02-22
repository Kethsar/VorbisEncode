using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VorbisEncode
{
    public partial class VorbisEncoder : IDisposable
    {
        private ogg_stream_state os; 
        private ogg_page og;
        private ogg_packet op; 
        private vorbis_info vi;
        private vorbis_comment vc; 
        private vorbis_block vb;
        private vorbis_dsp_state vd; 

        private Random m_rand;
        private bool m_bos; // Beginning of Stream
        private bool m_first;
        private Stream m_enc_stream;
        private byte[] m_enc_buf;
        private long m_readPos, m_writePos, m_stream_threshold;
        private static object m_locker = new object();

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in VBR mode,
        /// with none of the parameters filled in.
        /// </summary>
        public VorbisEncoder()
        {
            m_rand = new Random();
            Mode = BitrateMode.VBR;
            m_enc_stream = null;
            m_bos = m_first = true;
            m_readPos = m_writePos = 0;
        }

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in VBR mode.
        /// </summary>
        /// <param name="channels">The number of audio channels. Usually 1 or 2</param>
        /// <param name="samplerate">The sample rate to encode in. Must match input sample rate.</param>
        /// <param name="quality">Determines the quality of the encoding. 0.0f - 1.0f</param>
        public VorbisEncoder(int channels, int samplerate, float quality) : this()
        {
            Channels = channels;
            SampleRate = samplerate;
            Quality = quality;
        }

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in CBR mode.
        /// </summary>
        /// <param name="channels">The number of audio channels. Usually 1 or 2</param>
        /// <param name="samplerate">The sample rate to encode in. Must match input sample rate.</param>
        /// <param name="bitrate">The bitrate to encode at, in kbps (e.g. 192 for 192kbps)</param>
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

        public int Bitrate { get; set; }
        public float Quality { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int State { get; set; }
        public BitrateMode Mode { get; set; } 
        public bool EOS { get; private set; }
        public Dictionary<string, string> MetaData { get; set; }

        public int vorbis_enc_init(Dictionary<string, string> meta = null)
        {
            int ret = -1;
            EOS = false;
            m_bos = true;
            m_first = false;

            /********** Encode setup ************/

            vorbis_info_init(ref vi);

            if (Mode == BitrateMode.CBR)
                ret = vorbis_encode_init(ref vi, Channels, SampleRate, Bitrate * 1000, Bitrate * 1000, Bitrate * 1000);
            else if (Mode == BitrateMode.VBR)
                ret = vorbis_encode_init_vbr(ref vi, Channels, SampleRate, Quality);

            if (ret != 0) return ret;

            /* add comments */
            vorbis_comment_init(ref vc);

            if (meta != null)
            {
                foreach (var tag in meta.Keys)
                {
                    vorbis_comment_add_tag(ref vc, tag, meta[tag]);
                }
            }

            /* set up the analysis state and auxiliary encoding storage */
            vorbis_analysis_init(ref vd, ref vi);
            vorbis_block_init(ref vd, ref vb);

            return 0;
        }

        //This function needs to be called before
        //every connection
        public void vorbis_enc_write_header()
        {

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

        public int vorbis_enc_reinit(Dictionary<string, string> meta = null)
        {
            vorbis_enc_close();
            return vorbis_enc_init(meta);
        }

        public int vorbis_enc_encode(byte[] pcm_buf, byte[] enc_buf, int size)
        {
            int result;
            int encoded_bytes = 0;
            

            /* This ensures the actual
             * audio data will start on a new page, as per spec
             */
            while (!EOS && m_bos)
            {
                result = ogg_stream_flush(ref os, ref og);

                if (result == 0)
                {
                    m_bos = false;
                    break;
                }
                
                Marshal.Copy(og.header, enc_buf, encoded_bytes, og.header_len);
                encoded_bytes += og.header_len;
                Marshal.Copy(og.body, enc_buf, encoded_bytes, og.body_len);
                encoded_bytes += og.body_len;
            }


            if (size == 0)
            {
                vorbis_analysis_wrote(ref vd, 0);
            }
            else
            {
                int i;

                unsafe
                {
                    float** vab = (float**)(vorbis_analysis_buffer(ref vd, size).ToPointer());

                    //deinterlace audio data and convert it from short to float
                    if (Channels == 2) // stereo
                    {
                        for (i = 0; i < size / 4; i++)
                        {
                            vab[0][i] = (BitConverter.ToInt16(pcm_buf, i * 4) / 32768f);
                            vab[1][i] = (BitConverter.ToInt16(pcm_buf, i * 4 + 2) / 32768f);
                        }
                    }
                    else // mono
                    {
                        for (i = 0; i < size; i++)
                        {
                            vab[0][i] = pcm_buf[i] / 32768f;
                        }
                    }
                }

                // Tell libvorbis how much data we actually wrote to the buffer
                vorbis_analysis_wrote(ref vd, i);
            }

            while (vorbis_analysis_blockout(ref vd, ref vb) == 1)
            {
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
                        
                        Marshal.Copy(og.header, enc_buf, encoded_bytes, og.header_len);
                        encoded_bytes += og.header_len;
                        Marshal.Copy(og.body, enc_buf, encoded_bytes, og.body_len);
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

        public void vorbis_enc_close()
        {
            /* clean up . vorbis_info_clear() must be called last */
            ogg_stream_clear(ref os);
            vorbis_block_clear(ref vb);
            vorbis_dsp_clear(ref vd);
            vorbis_comment_clear(ref vc);
            vorbis_info_clear(ref vi);
        }

        public void EncodeStream(Stream stdin, Stream stdout)
        {
            var audio_buf = new byte[SampleRate * 10];
            m_enc_buf = new byte[SampleRate * 10];
            int bytes_read, enc_bytes_read;

            vorbis_enc_reinit(MetaData);
            vorbis_enc_write_header();

            while (!EOS)
            {
                bytes_read = stdin.Read(audio_buf, 0, audio_buf.Length);
                enc_bytes_read = vorbis_enc_encode(audio_buf, m_enc_buf, bytes_read);

                stdout.Write(m_enc_buf, 0, enc_bytes_read);
                stdout.Flush();
            }
        }

        public Task EncodeStreamAsync(Stream stdin, Stream stdout)
        {
            return Task.Run(() => EncodeStream(stdin, stdout));
        }

        public void PutBytes(byte[] audioBuffer, int count)
        {
            int enc_bytes_read = 0;

            if (m_enc_buf == null)
                m_enc_buf = new byte[SampleRate * 10];

            if (m_enc_stream == null)
            {
                m_enc_stream = new MemoryStream();
                m_stream_threshold = SampleRate * Channels * 10;
            }

            if (m_bos)
            {
                if (!m_first && !EOS)
                {
                    enc_bytes_read = vorbis_enc_encode(audioBuffer, m_enc_buf, 0);
                    WriteToEncStream(m_enc_buf, enc_bytes_read);
                }
                
                vorbis_enc_reinit(MetaData);
                vorbis_enc_write_header();
            }

            enc_bytes_read = vorbis_enc_encode(audioBuffer, m_enc_buf, count);

            WriteToEncStream(m_enc_buf, enc_bytes_read);
        }

        public int GetBytes(byte[] buffer, int count)
        {
            return ReadFromEncStream(buffer, count);
        }

        private void WriteToEncStream(byte[] enc_buf, int count)
        {
            lock (m_locker)
            {
                m_enc_stream.Position = m_writePos;
                m_enc_stream.Write(enc_buf, 0, count);
                m_enc_stream.Flush();
                m_writePos = m_enc_stream.Position;
            }
        }

        private int ReadFromEncStream(byte[] buffer, int count)
        {
            int read_bytes = 0;

            lock (m_locker)
            {
                m_enc_stream.Position = m_readPos;
                read_bytes = m_enc_stream.Read(buffer, 0, count);
                m_readPos = m_enc_stream.Position;

                if (m_readPos >= m_stream_threshold)
                {
                    var newStream = new MemoryStream();
                    m_enc_stream.CopyTo(newStream);
                    m_readPos = 0;
                    m_writePos = newStream.Position;
                    m_enc_stream = newStream;
                }
            }

            return read_bytes;
        }

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
            vorbis_enc_close();

            if(disposing)
            {
                if (m_enc_stream != null)
                {
                    m_enc_stream.Dispose();
                    m_enc_stream = null;
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
