using System;
using System.Collections.Generic;
using System.IO;
using VorbisEncode;

namespace TestEncoding
{
    class Program
    {
        const int SIZE = 1024 * 4;
        static void Main(string[] args)
        {
            /*
            var t = typeof(vorbis_block);

            Console.WriteLine(Marshal.SizeOf(typeof(vorbis_dsp_state)));
            Console.WriteLine(Marshal.SizeOf(t));

            foreach (var x in typeof(vorbis_block).GetFields())
            {
                Console.WriteLine("{0,15}: {1}", x.Name, Marshal.OffsetOf(t, x.Name));
            }
            */

            var ve = new VorbisEncoder(2, 44100, 0.7f);
            var audio_buf = new byte[SIZE * 4];
            var enc_buf = new byte[SIZE * 4];
            var oggHeaderWritten = false;
            var bytes_written = 0;
            var files = new string[] { @"unencoded.raw", @"lsm.wav" };
            var datas = new List<Dictionary<string, string>>();
            var cnt = 0;

            var meta = new Dictionary<string, string>();
            meta.Add("ARTIST", "No idea");
            meta.Add("TITLE", "I got this song from some sample code");
            meta.Add("ALBUM", "seriously what");
            meta.Add("DATE", "???");
            meta.Add("ENCODER", "Kethsar");

            datas.Add(meta);

            meta = new Dictionary<string, string>();
            meta.Add("ARTIST", "Lite Show Magic");
            meta.Add("TITLE", "We Are LSM");
            meta.Add("ALBUM", "We are \"Lite Show Magic\"");
            meta.Add("DATE", "2017");
            meta.Add("ENCODER", "Kethsar");

            datas.Add(meta);

            var stdout = new FileStream(@"encoded2.ogg", FileMode.Create, FileAccess.Write);

            foreach (var file in files)
            {
                var stdin = new FileStream(file, FileMode.Open, FileAccess.Read);

                ve.vorbis_enc_reinit(datas[cnt]);
                oggHeaderWritten = false;

                if (!oggHeaderWritten)
                {
                    ve.vorbis_enc_write_header();
                    oggHeaderWritten = true;
                }

                while (ve.OggState.e_o_s != 1)
                {
                    var bytes_read = stdin.Read(audio_buf, 0, audio_buf.Length);
                    var enc_bytes_read = ve.vorbis_enc_encode(audio_buf, enc_buf, bytes_read);

                    stdout.Write(enc_buf, 0, enc_bytes_read);
                    bytes_written += enc_bytes_read;

                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine("KB written: {0:N2}KB", bytes_written / 1024.0);
                }

                stdin.Close();
                cnt++;
            }

            stdout.Close();
            ve.Dispose();

            Console.ReadKey(true);
        }
    }
}
