using System;
using System.IO;

namespace VorbisEncode
{
    /// <summary>
    /// Thin <see cref="Stream"/> around <see cref="VorbisEncoder"/>
    /// </summary>
    public class VorbisEncoderStream : Stream, IDisposable
    {
        private VorbisEncoder m_ve;

        public VorbisEncoderStream()
        {
            m_ve = new VorbisEncoder();
        }

        public VorbisEncoderStream(int channels, int samplerate, float quality)
        {
            m_ve = new VorbisEncoder(channels, samplerate, quality);
        }

        public VorbisEncoderStream(int channels, int samplerate, int bitrate)
        {
            m_ve = new VorbisEncoder(channels, samplerate, bitrate);
        }

        /*
        ~VorbisEncoderStream()
        {
            Dispose(false);
        }
        */

        public VorbisEncoder Encoder
        {
            get
            {
                return m_ve;
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                return (m_ve.Buffer != null) ? m_ve.Buffer.Size : 0;
            }
        }

        public override long Position
        {
            get
            {
                return 0;
            }

            set
            {
                throw new InvalidOperationException("Let the VorbisEncoder internal buffer handle position.");
            }
        }

        public override void Flush()
        {
            return;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return m_ve.GetBytes(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("This is just a wrapper around an encoder and its shitty RingBuffer, what do you want?");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Non-resizable stream.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_ve.PutBytes(buffer, offset, count);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            m_ve.Dispose();
            base.Dispose(disposing);
        }
    }
}
