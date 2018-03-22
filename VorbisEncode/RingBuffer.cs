using System;

namespace VorbisEncode
{
    public class RingBuffer
    {
        private readonly object m_lock = new object();
        private byte[] m_buffer;

        public RingBuffer (long size)
        {
            Size = size;
            m_buffer = new byte[size];
        }

        public long Size { get; }
        public long Head { get; private set; }
        public long Tail { get; private set; }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;

            lock (m_lock)
            {
                if (CanWrite(count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        m_buffer[Head] = buffer[offset + i];
                        Head = (Head + 1) % Size;
                    }

                    if (Head == Tail)
                        Tail = (Tail + 1) % Size;
                }
                else
                    throw new Exception("Buffer too full");
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int readBytes = 0;

            if (!IsEmpty())
            {
                lock (m_lock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        buffer[offset + i] = m_buffer[Tail];

                        Tail++;
                        readBytes++;

                        if (Tail >= Size)
                            Tail = 0;

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

        public bool CanWrite(int count)
        {
            return GetSpaceLeft() >= count;
        }

        public long GetSpaceLeft()
        {
            long left = 0;

            if (!IsFull())
            {
                left = Size - (((Head + 1) % Size) - (Tail % Size));
            }

            return left;
        }
    }
}
