using System;
using System.Collections.Generic;
using System.IO;
using VorbisEncode;
using System.Reflection;

namespace TestEncoding
{
    class Program
    {
        const int SIZE = 1024 * 4;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.ToLower() == "make32")
                {
                    Make32();
                    Environment.Exit(0);
                }
            }

            string curPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                   path32 = Path.Combine(curPath, "32"),
                   path64 = Path.Combine(curPath, "64"),
                   vorbis32 = Path.Combine(path32, "libvorbis.dll"),
                   vorbis64 = Path.Combine(path64, "libvorbis.dll");

            if (!File.Exists(vorbis32))
            {
                Directory.CreateDirectory(path32);
                using (Stream instream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TestEncoding.lib.libvorbis32.dll"),
                              outstream = File.OpenWrite(vorbis32))
                {
                    instream.CopyTo(outstream);
                }
            }

            if (!File.Exists(vorbis64))
            {
                Directory.CreateDirectory(path64);
                using (Stream instream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TestEncoding.lib.libvorbis64.dll"),
                              outstream = File.OpenWrite(vorbis64))
                {
                    instream.CopyTo(outstream);
                }
            }

            if (Environment.Is64BitProcess) LoadLibrary(vorbis64);
            else LoadLibrary(vorbis32);

            VorbisEncoderStream ves = new VorbisEncoderStream(2, 44100, 0.7f);
            var files = new string[] { @"lsm.wav" };
            var datas = new List<Dictionary<string, string>>();

            var meta = new Dictionary<string, string>();
            meta = new Dictionary<string, string>();
            meta.Add("ARTIST", "Lite Show Magic");
            meta.Add("TITLE", "We Are LSM");
            meta.Add("ALBUM", "We are \"Lite Show Magic\"");
            meta.Add("DATE", "2017");
            meta.Add("ENCODER", "Kethsar");

            datas.Add(meta);

            for (int i = 0; i < 20; i++)
            {
                Console.WriteLine($"\nByte In/Out {i+1}:\n");
                ves = new VorbisEncoderStream(2, 44100, 0.7f);
                VorbisEncoderStreamExample(ves, datas[0], files[0]);
                Console.WriteLine($"{GC.GetTotalMemory(false) / 1024 / 1024}MB allocated");
            }

            ves.Dispose();
            GC.Collect();
            Console.WriteLine($"{GC.GetTotalMemory(true) / 1024 / 1024}MB allocated");

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        // For creating a 32-bit only exe to test with
        // Code courtesy of ed @ rizon
        private static void Make32()
        {
            string fo = Assembly.GetExecutingAssembly().Location;
            using (FileStream i = new FileStream(fo, FileMode.Open, FileAccess.Read))
            {
                fo = fo.Substring(0, fo.LastIndexOf('.')) + "32.exe";
                using (FileStream o = new FileStream(fo, FileMode.Create))
                {
                    bool first = true;
                    byte[] buf = new byte[8192];
                    while (true)
                    {
                        int n = i.Read(buf, 0, buf.Length);
                        if (first)
                        {
                            first = false;
                            buf[0x218] = 3; //1=any
                        }
                        if (n <= 0) break;
                        o.Write(buf, 0, n);
                    }
                }
            }
        }

        private static void VorbisEncoderStreamExample(VorbisEncoderStream ves, Dictionary<string, string> meta, string file)
        {
            byte[] audio_buf = new byte[SIZE], enc_buf = new byte[SIZE];
            var fi = file + "_byte-in-out.ogg";
            long bytes_written = 0;
            int bytes_read = 0;

            Console.WriteLine($"Starting encode for {file} to {fi}");

            using (FileStream stdin = new FileStream(file, FileMode.Open, FileAccess.Read),
                        stdout = new FileStream(fi, FileMode.Create, FileAccess.Write))
            {
                if (file.EndsWith("wav", StringComparison.CurrentCultureIgnoreCase))
                    IgnoreWavHeader(stdin);

                ves.Encoder.ChangeMetaData(meta);

                do
                {
                    bytes_read = stdin.Read(audio_buf, 0, SIZE);
                    ves.Write(audio_buf, 0, bytes_read);

                    var enc_bytes_read = ves.Read(enc_buf, 0, enc_buf.Length);
                    stdout.Write(enc_buf, 0, enc_bytes_read);
                    bytes_written += enc_bytes_read;

                    Console.Write($"\rKB Written: {(bytes_written / 1024.0):N2}");
                } while (bytes_read > 0);
            }

            Console.WriteLine($"\nFinished encode for {file}\n");
        }


        private static void ByteInOutExample(VorbisEncoder ve, Dictionary<string, string> meta, string file)
        {
            byte[] audio_buf = new byte[SIZE], enc_buf = new byte[SIZE];
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
                    bytes_read = stdin.Read(audio_buf, 0, SIZE);
                    ve.PutBytes(audio_buf, 0, bytes_read);

                    var enc_bytes_read = ve.GetBytes(enc_buf, 0, enc_buf.Length);
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

        // We are testing, we don't care to check if the wav file matches 16-bit 44100
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
