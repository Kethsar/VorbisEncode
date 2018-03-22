using System;
using System.IO;
using System.Threading;

namespace VorbisEncode
{
    /// <summary>
    /// Thin <see cref="Stream"/> around <see cref="VorbisEncoder"/>
    /// </summary>
    public class VorbisEncoderStream : Stream, IDisposable
    {
        private VorbisEncoder m_ve;
        private readonly object m_lock = new object();

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
                return m_ve.Buffer != null && !m_ve.Buffer.IsEmpty();
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
                return m_ve.Buffer != null && !m_ve.Buffer.IsFull();
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
            int bytesRead = 0, lockwaits = 0;

            lock (m_lock)
            {
                while (!CanRead && lockwaits < 10)
                {
                    lockwaits++;
                    Monitor.Wait(m_lock, 1);
                }

                bytesRead = m_ve.GetBytes(buffer, offset, count);
            }

            return bytesRead;
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
            lock (m_lock)
            {
                while (!m_ve.Buffer.CanWrite(count))
                    Monitor.Wait(m_lock, 1);

                m_ve.PutBytes(buffer, offset, count);
            }
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
