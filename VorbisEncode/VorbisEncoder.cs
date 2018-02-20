using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VorbisEncode
{
    public partial class VorbisEncoder : IDisposable
    {
        private ogg_stream_state os; /* take physical pages, weld into a logical stream of packets */
        private ogg_page og; /* one Ogg bitstream page.  Vorbis packets are inside */
        private ogg_packet op; /* one raw packet of data for decode */
        private vorbis_info vi;
        private vorbis_comment vc; /* struct that stores all the user comments */
        private vorbis_block vb;
        private vorbis_dsp_state vd; /* central working state for the packet->PCM decoder */

        private Random rand;

        /// <summary>
        /// Initialize a new instance of the VorbisEncoder class in VBR mode,
        /// with none of the parameters filled in.
        /// </summary>
        public VorbisEncoder()
        {
            rand = new Random();
            Mode = BitrateMode.VBR;
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

        public ogg_stream_state OggState { get { return os; } }
        public ogg_page OggPage { get { return og; } }
        public ogg_packet OggPacket { get { return op; } }
        public vorbis_info VorbisInfo { get { return vi; } }
        public vorbis_comment VorbisComment { get { return vc; } }
        public vorbis_block VorbisBlock { get { return vb; } }
        public vorbis_dsp_state VorbisDSPState { get { return vd; } }

        public int Bitrate { get; set; }
        public float Quality { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int State { get; set; }
        public BitrateMode Mode { get; set; } 

        public int vorbis_enc_init(Dictionary<string, string> meta = null)
        {
            int ret = -1;

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
            ogg_stream_init(ref os, rand.Next());
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
            int i, result;
            int eos = 0;
            int w = 0;
            

            /* This ensures the actual
             * audio data will start on a new page, as per spec
             */
            while (eos == 0)
            {
                result = ogg_stream_flush(ref os, ref og);

                if (result == 0)
                    break;
                
                Marshal.Copy(og.header, enc_buf, w, og.header_len);
                w += og.header_len;
                Marshal.Copy(og.body, enc_buf, w, og.body_len);
                w += og.body_len;
            }


            if (size == 0)
            {
                vorbis_analysis_wrote(ref vd, 0);
            }
            else
            {
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
                    while (eos == 0)
                    {
                        result = ogg_stream_pageout(ref os, ref og);
                        if (result == 0)
                            break;
                        
                        Marshal.Copy(og.header, enc_buf, w, og.header_len);
                        w += og.header_len;
                        Marshal.Copy(og.body, enc_buf, w, og.body_len);
                        w += og.body_len;

                        if (ogg_page_eos(ref og) != 0)
                        {
                            eos = 1;
                            os.e_o_s = 1;
                        }
                    }
                }
            }

            return w;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            vorbis_enc_close();
        }

        public enum BitrateMode
        {
            VBR,
            CBR
        };
    }
}
