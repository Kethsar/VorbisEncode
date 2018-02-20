using System;
using System.Runtime.InteropServices;

namespace VorbisEncode
{
    [StructLayout(LayoutKind.Sequential)]
    public struct oggpack_buffer
    {
        public int endbyte;
        public int endbit;

        /// void*
        public IntPtr buffer;

        /// void*
        public IntPtr ptr;
        public int storage;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ogg_page
    {
        /// void*
        public IntPtr header;
        public int header_len;

        /// void*
        public IntPtr body;
        public int body_len;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public unsafe struct ogg_stream_state
    {
        /// void*
        public IntPtr body_data;    /* bytes from packet bodies */
        public int body_storage;          /* storage elements allocated */
        public int body_fill;             /* elements stored; fill mark */
        public int body_returned;         /* elements of fill returned */
        
        /// int*
        public IntPtr lacing_vals;      /* The values that will go to the segment table */

        /// ogg_int64_t*
        public IntPtr granule_vals; /* granulepos values for headers. Not compact
                                this way, but it is simple coupled to the
                                lacing fifo */
        public int lacing_storage;
        public int lacing_fill;
        public int lacing_packet;
        public int lacing_returned;

        public fixed byte header[282];      /* working space for header encode */
        public int header_fill;

        public int e_o_s;          /* set when we have buffered the last packet in the
                             logical bitstream */
        public int b_o_s;          /* set after we've written the initial page
                             of a logical bitstream */
        public int serialno;
        public int pageno;
        public long packetno;  /* sequence number for decode; the framing
                             knows where there's a hole in the data,
                             but we need coupling so that the codec
                             (which is in a separate abstraction
                             layer) also knows about the gap */
        public long granulepos;

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ogg_packet
    {
        /// void*
        public IntPtr packet;
        public int bytes;
        public int b_o_s;
        public int e_o_s;

        public long granulepos;

        public long packetno;     /* sequence number for decode; the framing
                                knows where there's a hole in the data,
                                but we need coupling so that the codec
                                (which is in a separate abstraction
                                layer) also knows about the gap */
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ogg_sync_state
    {

        /// void*
        public IntPtr data;
        public int storage;
        public int fill;
        public int returned;

        public int unsynced;
        public int headerbytes;
        public int bodybytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct vorbis_info
    {
        public int version;
        public int channels;
        public int rate;

        public int bitrate_upper;
        public int bitrate_nominal;
        public int bitrate_lower;
        public int bitrate_window;

        /// void*
        public IntPtr codec_setup;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct vorbis_dsp_state
    {
        public int analysisp;

        /// vorbis_info*
        public IntPtr vi;

        /// float**
        public IntPtr pcm;

        /// float**
        public IntPtr pcmret;
        public int pcm_storage;
        public int pcm_current;
        public int pcm_returned;

        public int preextrapolate;
        public int eofflag;

        public int lW;
        public int W;
        public int nW;
        public int centerW;

        public long granulepos;
        public long sequence;

        public long glue_bits;
        public long time_bits;
        public long floor_bits;
        public long res_bits;

        /// void*
        public IntPtr backend_state;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct vorbis_block
    {
        /* necessary stream state for linking to the framing abstraction */
        /// float**
        public IntPtr pcm;       /* this is a popublic inter public into local storage */
        public oggpack_buffer opb;

        public int lW;
        public int W;
        public int nW;
        public int pcmend;
        public int mode;

        public int eofflag;
        public long granulepos;
        public long sequence;

        /// vorbis_dsp_state*
        public IntPtr vd; /* For read-only access of configuration */

        /* local storage to avoid remallocing; it's up to the mapping to
           structure it */

        /// void*
        public IntPtr localstore;
        public int localtop;
        public int localalloc;
        public int totaluse;

        /// alloc_chain*
        public IntPtr reap;

        public int glue_bits;
        public int time_bits;
        public int floor_bits;
        public int res_bits;

        /// void*
        public IntPtr @internal;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct alloc_chain
    {
        /// void*
        public IntPtr ptr;

        /// alloc_chain*
        public IntPtr next;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct vorbis_comment
    {
        /* unlimited user comment fields.  libvorbis writes 'libvorbis'
           whatever vendor is set to in encode */

        /// char**
        public IntPtr user_comments;

        /// int*
        public IntPtr comment_lengths;
        public int comments;

        /// char*
        public IntPtr vendor;

    }
}
