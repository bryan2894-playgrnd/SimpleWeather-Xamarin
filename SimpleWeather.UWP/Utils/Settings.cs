﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleWeather.Utils
{
    public static partial class Settings
    {
        private static StorageFolder appDataFolder = ApplicationData.Current.LocalFolder;
        private static StorageFile dataFile;

        private static string getTempUnit()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("Units") || localSettings.Values["Units"] == null)
            {
                return Fahrenheit;
            }
            else if (localSettings.Values["Units"].Equals("C"))
                return Celsius;

            return Fahrenheit;
        }

        private static void setTempUnit(string value)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (value == Celsius)
                localSettings.Values["Units"] = Celsius;
            else
                localSettings.Values["Units"] = Fahrenheit;
        }

        private static bool isWeatherLoaded()
        {
            if (dataFile == null)
                dataFile = appDataFolder.CreateFileAsync("data.json", CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter().GetResult();

            FileInfo fileinfo = new FileInfo(dataFile.Path);

            if (fileinfo.Length == 0 || !fileinfo.Exists)
            {
                setWeatherLoaded(false);
                return false;
            }

            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values["weatherLoaded"] == null)
            {
                return false;
            }
            else if (localSettings.Values["weatherLoaded"].Equals("true"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void setWeatherLoaded(bool isLoaded)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (isLoaded)
            {
                localSettings.Values["weatherLoaded"] = "true";
            }
            else
            {
                localSettings.Values["weatherLoaded"] = "false";
            }
        }

        private static string getAPI()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("API") || localSettings.Values["API"] == null)
            {
                setAPI("WUnderground");
                return "WUnderground";
            }
            else
                return (string)localSettings.Values["API"];
        }

        private static void setAPI(string value)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["API"] = value;
        }

        public static async Task<List<string>> getLocations()
        {
            OrderedDictionary dict = await getWeatherData();
            List<string> locations = new List<string>();
            foreach (string location in dict.Keys)
            {
                locations.Add(location);
            }

            return locations;
        }

        public static async Task<OrderedDictionary> getWeatherData()
        {
            if (dataFile == null)
                dataFile = await appDataFolder.CreateFileAsync("data.json", CreationCollisionOption.OpenIfExists);

            FileInfo fileinfo = new FileInfo(dataFile.Path);

            if (fileinfo.Length == 0 || !fileinfo.Exists)
                return new OrderedDictionary();

            return (OrderedDictionary)JSONParser.Deserializer(await FileUtils.ReadFile(dataFile), typeof(OrderedDictionary));
        }

        public static async void saveWeatherData(OrderedDictionary weatherData)
        {
            if (dataFile == null)
                dataFile = await appDataFolder.CreateFileAsync("data.json", CreationCollisionOption.OpenIfExists);

            JSONParser.Serializer(weatherData, dataFile);
        }

        #region WeatherUnderground
        private static string getAPIKEY()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("API_KEY") || localSettings.Values["API_KEY"] == null)
            {
                String key = String.Empty;
                key = readAPIKEYfile().GetAwaiter().GetResult();

                if (!String.IsNullOrWhiteSpace(key))
                    setAPIKEY(key);

                return key;
            }
            else
                return (string)localSettings.Values["API_KEY"];
        }

        private static async Task<string> readAPIKEYfile()
        {
            // Read key from file
            String key = String.Empty;
            try
            {
                StorageFile keyFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///API_KEY.txt"));
                FileInfo fileinfo = new FileInfo(keyFile.Path);

                if (fileinfo.Length != 0 || fileinfo.Exists)
                {
                    StreamReader reader = new StreamReader(await keyFile.OpenStreamForReadAsync());
                    key = reader.ReadLine();
                    reader.Dispose();
                }
            }
            catch (FileNotFoundException) { }

            return key;
        }

        private static void setAPIKEY(string API_KEY)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (!String.IsNullOrWhiteSpace(API_KEY))
                localSettings.Values["API_KEY"] = API_KEY;
        }
        #endregion
    }
}