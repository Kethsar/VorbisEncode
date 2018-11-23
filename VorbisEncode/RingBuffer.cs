using System;

namespace VorbisEncode
{
    public class RingBuffer
    {
        private readonly object m_lock = new object();
        private byte[] m_buffer;

        public RingBuffer(long size)
        {
            Size = size;
            m_buffer = new byte[size];
        }

        public long Size { get; private set; }
        public long Head { get; private set; }
        public long Tail { get; private set; }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;

            lock (m_lock)
            {
                if (NeedsResize(count))
                {
                    long size;

                    if (Size > (long.MaxValue / 2))
                        size = long.MaxValue;
                    else
                        size = Size * 2;

                    Resize(size);
                }

                for (int i = 0; i < count && (i + offset) < buffer.Length; i++)
                {
                    m_buffer[Head] = buffer[offset + i];
                    Head = (Head + 1) % Size;
                }
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int readBytes = 0;

            if (!IsEmpty())
            {
                lock (m_lock)
                {
                    for (int i = 0; i < count && (i + offset) < buffer.Length; i++)
                    {
                        buffer[offset + i] = m_buffer[Tail];

                        Tail = (Tail + 1) % Size;
                        readBytes++;

                        if (IsEmpty())
                            break;
                    }
                }
            }

            return readBytes;
        }

        public void Reset()
        {
            lock (m_lock)
                Head = Tail;
        }

        public void Resize(long size)
        {
            lock (m_lock)
            {
                if (Size == long.MaxValue)
                    throw new ArgumentOutOfRangeException("size", "The size of the RingBuffer has reached the maximum size it can be. How did you manage that?");

                var cnt = Count();
                if (size < cnt)
                    throw new ArgumentOutOfRangeException("size", "The new size of the RingBuffer cannot be smaller than the amount of bytes currently stored.");

                var newBuf = new byte[size];

                if (IsEmpty())
                {
                    Head = 0;
                }
                else if (Tail < Head)
                {
                    Array.Copy(m_buffer, Tail, newBuf, 0, (Head - Tail));
                    Head = cnt;
                }
                else
                {
                    var tailCnt = Size - Tail;

                    Array.Copy(m_buffer, Tail, newBuf, 0, tailCnt);
                    Array.Copy(m_buffer, 0, newBuf, tailCnt, Head);
                    Head = cnt;
                }

                Size = size;
                Tail = 0;
                m_buffer = newBuf;
            }
        }

        public bool IsEmpty()
        {
            bool empty = false;

            lock (m_lock)
                empty = Head == Tail;

            return empty;
        }

        public bool IsFull()
        {
            bool full = false;

            lock (m_lock)
                full = ((Head + 1) % Size) == Tail;

            return full;
        }

        public bool NeedsResize(int count)
        {
            lock (m_lock)
                return GetSpaceLeft() < count;
        }

        public long Count()
        {
            lock (m_lock)
                return (Size - 1) - GetSpaceLeft();
        }

        public long GetSpaceLeft()
        {
            long left = 0;

            lock (m_lock)
            {
                if (!IsFull())
                {
                    if (Head >= Tail)
                        left = (Size - 1) - (Head - Tail);
                    else
                        left = Tail - Head - 1;
                }
            }

            return left;
        }
    }
}
