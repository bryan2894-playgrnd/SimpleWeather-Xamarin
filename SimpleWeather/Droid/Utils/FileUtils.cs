﻿#if __ANDROID__
using System;
using System.Text;
using System.Threading.Tasks;
using Java.IO;
using SimpleWeather.Droid;
using Android.Support.V4.Util;

namespace SimpleWeather.Utils
{
    public static partial class FileUtils
    {
        public async static Task<String> ReadFile(File file)
        {
            using (AtomicFile mFile = new AtomicFile(file))
            {
                // Wait for file to be free
                while (IsFileLocked(file))
                {
                    await Task.Delay(100);
                }

                String data;

                using (BufferedReader reader = new BufferedReader(new InputStreamReader(mFile.OpenRead())))
                {
                    String line = await reader.ReadLineAsync();
                    StringBuilder sBuilder = new StringBuilder();

                    while (line != null)
                    {
                        sBuilder.Append(line).Append("\n");
                        line = await reader.ReadLineAsync();
                    }

                    reader.Dispose();
                    data = sBuilder.ToString();
                }

                return data;
            }
        }

        public static async Task WriteFile(String data, File file)
        {
            using (AtomicFile mFile = new AtomicFile(file))
            {
                // Wait for file to be free
                while (IsFileLocked(file))
                {
                    await Task.Delay(100);
                }

                using (System.IO.Stream outputStream = mFile.StartWrite())
                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(outputStream))
                {
                    // Clear file before writing
                    outputStream.SetLength(0);

                    await writer.WriteAsync(data);
                    await writer.FlushAsync();
                    mFile.FinishWrite(outputStream);
                }
            }
        }

        public static bool IsFileLocked(File file)
        {
            if (!file.Exists())
                return false;

            FileInputStream stream = null;

            try
            {
                stream = new FileInputStream(file);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }

            //file is not locked
            return false;
        }
    }
}
#endif
