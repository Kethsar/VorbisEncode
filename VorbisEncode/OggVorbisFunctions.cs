using System;
using System.Runtime.InteropServices;

namespace VorbisEncode
{
    public partial class VorbisEncoder
    {
        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_info_init")]
        public static extern void vorbis_info_init(ref vorbis_info vi);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_encode_init")]
        public static extern int vorbis_encode_init(ref vorbis_info vi, int channels, int rate, int max_bitrate, int nominal_bitrate, int min_bitrate);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_encode_init_vbr")]
        public static extern int vorbis_encode_init_vbr(ref vorbis_info vi, int channels, int rate, float base_quality);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_comment_init")]
        public static extern void vorbis_comment_init(ref vorbis_comment vc);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_comment_add_tag")]
        public static extern void vorbis_comment_add_tag(ref vorbis_comment vc, [In()] [MarshalAs(UnmanagedType.LPStr)] string tag, [In()] [MarshalAs(UnmanagedType.LPStr)] string contents);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_init")]
        public static extern int vorbis_analysis_init(ref vorbis_dsp_state v, ref vorbis_info vi);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_block_init")]
        public static extern int vorbis_block_init(ref vorbis_dsp_state v, ref vorbis_block vb);

        [DllImport("libogg.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_init")]
        public static extern int ogg_stream_init(ref ogg_stream_state os, int serialno);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_headerout")]
        public static extern int vorbis_analysis_headerout(ref vorbis_dsp_state v, ref vorbis_comment vc, ref ogg_packet op, ref ogg_packet op_comm, ref ogg_packet op_code);

        [DllImport("libogg.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_packetin")]
        public static extern int ogg_stream_packetin(ref ogg_stream_state os, ref ogg_packet op);

        [DllImport("libogg.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_flush")]
        public static extern int ogg_stream_flush(ref ogg_stream_state os, ref ogg_page og);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_buffer")]
        public static extern IntPtr vorbis_analysis_buffer(ref vorbis_dsp_state v, int vals);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_wrote")]
        public static extern int vorbis_analysis_wrote(ref vorbis_dsp_state v, int vals);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_blockout")]
        public static extern int vorbis_analysis_blockout(ref vorbis_dsp_state v, ref vorbis_block vb);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis")]
        public static extern int vorbis_analysis(ref vorbis_block vb, ref ogg_packet op);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis")]
        public static extern int vorbis_analysis(ref vorbis_block vb, IntPtr op);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_bitrate_addblock")]
        public static extern int vorbis_bitrate_addblock(ref vorbis_block vb);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_bitrate_flushpacket")]
        public static extern int vorbis_bitrate_flushpacket(ref vorbis_dsp_state vd, ref ogg_packet op);

        [DllImport("libogg.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_pageout")]
        public static extern int ogg_stream_pageout(ref ogg_stream_state os, ref ogg_page og);

        [DllImport("libogg.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_page_eos")]
        public static extern int ogg_page_eos(ref ogg_page og);

        [DllImport("libogg.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_clear")]
        public static extern int ogg_stream_clear(ref ogg_stream_state os);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_block_clear")]
        public static extern int vorbis_block_clear(ref vorbis_block vb);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_dsp_clear")]
        public static extern void vorbis_dsp_clear(ref vorbis_dsp_state v);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_comment_clear")]
        public static extern void vorbis_comment_clear(ref vorbis_comment vc);

        [DllImport("libvorbis.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_info_clear")]
        public static extern void vorbis_info_clear(ref vorbis_info vi);
    }
}