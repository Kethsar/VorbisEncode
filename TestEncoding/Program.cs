using System;
using System.Collections.Generic;
using System.IO;
using VorbisEncode;

namespace TestEncoding
{
    class Program
    {
        static void Main(string[] args)
        {
            var ve = new VorbisEncoder(2, 44100, 0.7f);
            var files = new string[] { @"lsm.wav", @"unencoded.raw" };
            var datas = new List<Dictionary<string, string>>();
            var cnt = 0;
            var stdout = new FileStream("continuous.ogg", FileMode.Create, FileAccess.Write);

            var meta = new Dictionary<string, string>();
            meta.Add("ARTIST", "Lite Show Magic");
            meta.Add("TITLE", "We Are LSM");
            meta.Add("ALBUM", "We are \"Lite Show Magic\"");
            meta.Add("DATE", "2017");
            meta.Add("ENCODER", "Kethsar");

            datas.Add(meta);

            meta = new Dictionary<string, string>();
            meta.Add("ARTIST", "No idea");
            meta.Add("TITLE", "I got this song from some sample code");
            meta.Add("ALBUM", "seriously what");
            meta.Add("DATE", "???");
            meta.Add("ENCODER", "Kethsar");

            datas.Add(meta);

            foreach (var file in files)
            {
                Console.WriteLine("\nEncode Stream example:\n");
                EncodeStreamExample(ve, datas[cnt], file);

                Console.WriteLine("\nEncode Stream Async example:\n");
                EncodeStreamAsyncExample(ve, datas[cnt], file);

                Console.WriteLine("\nByte In/Out example:\n");
                ByteInOutExample(ve, datas[cnt], file);

                Console.WriteLine("\nEncode Continuous Stream example:\n");
                EncodeStreamContinuousExample(ve, datas[cnt], stdout, file);

                cnt++;
            }

            stdout.Dispose();
            ve.Dispose();

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static void ByteInOutExample(VorbisEncoder ve, Dictionary<string, string> meta, string file)
        {
            byte[] audio_buf = new byte[ve.SampleRate * 10], enc_buf = new byte[ve.SampleRate * 10];
            var fi = file + "_byte-in-out.ogg";
            long bytes_written = 0;
            int bytes_read = 0;

            Console.WriteLine($"Starting encode for {file} to {fi}");

            using (FileStream stdin = new FileStream(file, FileMode.Open, FileAccess.Read),
                        stdout = new FileStream(fi, FileMode.Create, FileAccess.Write))
            {
                if (file.EndsWith("wav", StringComparison.CurrentCultureIgnoreCase))
                    IgnoreWavHeader(stdin);

                ve.ChangeMetaData(meta);

                do
                {
                    bytes_read = stdin.Read(audio_buf, 0, audio_buf.Length);
                    ve.PutBytes(audio_buf, bytes_read);

                    var enc_bytes_read = ve.GetBytes(enc_buf, enc_buf.Length);
                    stdout.Write(enc_buf, 0, enc_bytes_read);
                    bytes_written += enc_bytes_read;

                    Console.Write($"\rKB Written: {(bytes_written / 1024.0):N2}");
                } while (bytes_read > 0);
            }

            Console.WriteLine($"\nFinished encode for {file}\n");
        }

        private static void EncodeStreamExample(VorbisEncoder ve, Dictionary<string, string> meta, string file)
        {
            var fi = file + "_stream.ogg";

            Console.WriteLine($"Starting encode for {file} to {fi}");

            using (FileStream stdin = new FileStream(file, FileMode.Open, FileAccess.Read),
                        stdout = new FileStream(fi, FileMode.Create, FileAccess.Write))
            {
                if (file.EndsWith("wav", StringComparison.CurrentCultureIgnoreCase))
                    IgnoreWavHeader(stdin);

                ve.ChangeMetaData(meta);

                ve.EncodeStream(stdin, stdout);
            }

            Console.WriteLine($"\nFinished encode for {file}\n");
        }

        private static void EncodeStreamAsyncExample(VorbisEncoder ve, Dictionary<string, string> meta, string file)
        {
            var fi = file + "_stream-async.ogg";

            Console.WriteLine($"Starting encode for {file} to {fi}");

            using (FileStream stdin = new FileStream(file, FileMode.Open, FileAccess.Read),
                        stdout = new FileStream(fi, FileMode.Create, FileAccess.Write))
            {
                if (file.EndsWith("wav", StringComparison.CurrentCultureIgnoreCase))
                    IgnoreWavHeader(stdin);

                ve.ChangeMetaData(meta);
                var encodeTask = ve.EncodeStreamAsync(stdin, stdout);

                while (!encodeTask.IsCompleted)
                {
                    Console.Write($"\rKB written: {(stdout.Position / 1024.0):N2}");
                    System.Threading.Thread.Sleep(100);
                }
            }

            Console.WriteLine($"\nFinished encode for {file}\n");
        }

        private static void EncodeStreamContinuousExample(VorbisEncoder ve, Dictionary<string, string> meta, FileStream stdout, string file)
        {
            var fi = new FileInfo(stdout.Name).Name;
            Console.WriteLine($"Starting encode for {file} to {fi}");

            using (FileStream stdin = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                if (file.EndsWith("wav", StringComparison.CurrentCultureIgnoreCase))
                    IgnoreWavHeader(stdin);

                ve.ChangeMetaData(meta);
                var encodeTask = ve.EncodeStreamAsync(stdin, stdout);

                while (!encodeTask.IsCompleted)
                {
                    Console.Write($"\rTotal KB written: {(stdout.Position / 1024.0):N2}");
                    System.Threading.Thread.Sleep(100);
                }
            }

            Console.WriteLine($"\nFinished encode for {file}\n");
        }

        // We are testing, we don't care to check
        private static void IgnoreWavHeader(FileStream strim)
        {
            byte[] buf = new byte[10];
            for(var i = 0; i < 30; i++)
            {
                strim.Read(buf, 0, 2);
                var txt = System.Text.Encoding.ASCII.GetString(buf, 0, 2);

                if (txt == "da")
                {
                    strim.Read(buf, 0, 6);
                    break;
                }
            }
        }
    }
}
