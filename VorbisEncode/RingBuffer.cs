using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VorbisEncode
{
    public class RingBuffer
    {
        private object locker = new object();
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
            lock (locker)
            {
                if (GetSpaceLeft() >= count && count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        m_buffer[Head] = buffer[offset + i];
                        Head = (Head + 1) % Size;
                    }

                    if (Head == Tail)
                        Tail = (Tail + 1) % Size;
                }
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int readBytes = 0;

            if (!IsEmpty())
            {
                lock (locker)
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
            lock (locker)
                Head = Tail;
        }

        public bool IsEmpty()
        {
            return Head == Tail;
        }

        public bool IsFull()
        {
            return ((Head + 1) % Size) == Tail;
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
