using System;
using System.Runtime.InteropServices;

namespace VorbisEncode
{
    public partial class VorbisEncoder
    {
        public const string VORBIS_DLL = "libvorbis.dll";
        public const string OGG_DLL = "libvorbis.dll";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vi">vorbis_info</param>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_info_init")]
        public static extern void vorbis_info_init(IntPtr vi);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vi">vorbis_info</param>
        /// <param name="channels"></param>
        /// <param name="rate"></param>
        /// <param name="max_bitrate"></param>
        /// <param name="nominal_bitrate"></param>
        /// <param name="min_bitrate"></param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_encode_init")]
        public static extern int vorbis_encode_init(IntPtr vi, int channels, int rate, int max_bitrate, int nominal_bitrate, int min_bitrate);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vi">vorbis_info</param>
        /// <param name="channels"></param>
        /// <param name="rate"></param>
        /// <param name="base_quality"></param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_encode_init_vbr")]
        public static extern int vorbis_encode_init_vbr(IntPtr vi, int channels, int rate, float base_quality);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vc">vorbis_comment</param>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_comment_init")]
        public static extern void vorbis_comment_init(IntPtr vc);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vc">vorbis_comment</param>
        /// <param name="tag"></param>
        /// <param name="contents"></param>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_comment_add_tag")]
        public static extern void vorbis_comment_add_tag(IntPtr vc, [In()] [MarshalAs(UnmanagedType.LPStr)] string tag, [In()] [MarshalAs(UnmanagedType.LPStr)] string contents);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        /// <param name="vi">vorbis_info</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_init")]
        public static extern int vorbis_analysis_init(IntPtr v, IntPtr vi);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        /// <param name="vb">vorbis_block</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_block_init")]
        public static extern int vorbis_block_init(IntPtr v, IntPtr vb);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="os">ogg_stream_state</param>
        /// <param name="serialno"></param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_init")]
        public static extern int ogg_stream_init(IntPtr os, int serialno);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        /// <param name="vc">vorbis_comment</param>
        /// <param name="op">ogg_packet</param>
        /// <param name="op_comm">ogg_packet</param>
        /// <param name="op_code">ogg_packet</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_headerout")]
        public static extern int vorbis_analysis_headerout(IntPtr v, IntPtr vc, ref ogg_packet op, ref ogg_packet op_comm, ref ogg_packet op_code);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="os">ogg_stream_state</param>
        /// <param name="op">ogg_packet</param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_packetin")]
        public static extern int ogg_stream_packetin(IntPtr os, IntPtr op);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="os">ogg_stream_state</param>
        /// <param name="op"></param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_packetin")]
        public static extern int ogg_stream_packetin(IntPtr os, ref ogg_packet op);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="os">ogg_stream_state</param>
        /// <param name="og">ogg_packet</param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_flush")]
        public static extern int ogg_stream_flush(IntPtr os, IntPtr og);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        /// <param name="vals"></param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_buffer")]
        public static extern IntPtr vorbis_analysis_buffer(IntPtr v, int vals);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        /// <param name="vals"></param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_wrote")]
        public static extern int vorbis_analysis_wrote(IntPtr v, int vals);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        /// <param name="vb">vorbis_block</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis_blockout")]
        public static extern int vorbis_analysis_blockout(IntPtr v, IntPtr vb);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vb">vorbis_block</param>
        /// <param name="op">ogg_packet</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_analysis")]
        public static extern int vorbis_analysis(IntPtr vb, IntPtr op);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vb">vorbis_block</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_bitrate_addblock")]
        public static extern int vorbis_bitrate_addblock(IntPtr vb);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vd">vorbis_dsp_state</param>
        /// <param name="op">ogg_packet</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_bitrate_flushpacket")]
        public static extern int vorbis_bitrate_flushpacket(IntPtr vd, IntPtr op);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="os">ogg_stream_state</param>
        /// <param name="og">ogg_page</param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_pageout")]
        public static extern int ogg_stream_pageout(IntPtr os, IntPtr og);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="og">ogg_page</param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_page_eos")]
        public static extern int ogg_page_eos(IntPtr og);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="os">ogg_stream_state</param>
        /// <returns></returns>
        [DllImport(OGG_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ogg_stream_clear")]
        public static extern int ogg_stream_clear(IntPtr os);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vb">vorbis_block</param>
        /// <returns></returns>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_block_clear")]
        public static extern int vorbis_block_clear(IntPtr vb);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vorbis_dsp_state</param>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_dsp_clear")]
        public static extern void vorbis_dsp_clear(IntPtr v);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vc">vorbis_comment</param>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_comment_clear")]
        public static extern void vorbis_comment_clear(IntPtr vc);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vi">vorbis_info</param>
        [DllImport(VORBIS_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "vorbis_info_clear")]
        public static extern void vorbis_info_clear(IntPtr vi);
    }
}