﻿using Newtonsoft.Json;
using SimpleWeather.WeatherData;
using SQLite;
using SQLiteNetExtensionsAsync.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if WINDOWS_UWP
using Windows.Storage;
#elif __ANDROID__
using Android.App;
#endif

namespace SimpleWeather.Utils
{
    public partial class DBUtils
    {
#if !__ANDROID_WEAR__
        public static async Task MigrateDataJsonToDB(SQLiteAsyncConnection locationDB, SQLiteAsyncConnection weatherDB)
        {
#if WINDOWS_UWP
            var appDataFolder = ApplicationData.Current.LocalFolder;
#elif __ANDROID__
            Java.IO.File appDataFolder = Application.Context.FilesDir;
#endif
            var locFileInfo = new FileInfo(Path.Combine(appDataFolder.Path, "locations.json"));
            var dataFileInfo = new FileInfo(Path.Combine(appDataFolder.Path, "data.json"));

            if (!locFileInfo.Exists)
            {
                if (!dataFileInfo.Exists)
                    return; // No data to migrate
            }

#if WINDOWS_UWP
            var locDataFile = (await appDataFolder.TryGetItemAsync("locations.json")) as StorageFile;
            var dataFile = (await appDataFolder.TryGetItemAsync("data.json")) as StorageFile;
#elif __ANDROID__
            Java.IO.File locDataFile = new Java.IO.File(appDataFolder, "locations.json");
            Java.IO.File dataFile = new Java.IO.File(appDataFolder, "data.json");
#endif

            List<LocationData> locationData = null;
            if (locDataFile != null && locFileInfo.Exists && locFileInfo.Length > 0)
            {
                // Migrate to new structure
                try
                {
                    locationData = await JSONParser.DeserializerAsync<List<LocationData>>(locDataFile);
                }
                catch (JsonSerializationException jsEx)
                {
                    Logger.WriteLine(LoggerLevel.Error, jsEx, "SimpleWeather: DataMigration: location json error");
                    locationData = null;
                }

                await Settings.SaveLocationData(locationData);

#if __ANDROID__
                locDataFile.Delete();
                locDataFile.Dispose();
#elif WINDOWS_UWP
                await locDataFile.DeleteAsync();
                locDataFile = null;
#endif
            }

            if (dataFile != null && dataFileInfo.Exists && dataFileInfo.Length > 0)
            {
                OrderedDictionary oldWeather = null;
                try
                {
                    oldWeather = await JSONParser.DeserializerAsync<OrderedDictionary>(dataFile);
                }
                catch (JsonSerializationException jsEx)
                {
                    Logger.WriteLine(LoggerLevel.Error, jsEx, "SimpleWeather: DataMigration: weather json error");
                    if (oldWeather == null)
                        oldWeather = new OrderedDictionary();
                }

                // Setup location data if N/A
                if (locationData == null || locationData.Count == 0)
                {
                    List<LocationData> data = new List<LocationData>();
                    foreach (string query in oldWeather.Keys.Cast<string>().ToList())
                    {
                        var weather = oldWeather[query] as Weather;
                        var prov = WeatherManager.GetProvider(weather.source);
                        var qview = await prov.GetLocation(weather.query);

                        LocationData loc = new LocationData(qview);
                        data.Add(loc);
                    }

                    locationData = data;
                    await Settings.SaveLocationData(locationData);
                }

                // Add data
                var list = oldWeather.Values.Cast<Weather>();
                await weatherDB.InsertOrReplaceAllWithChildrenAsync(list);

                // Delete old files
#if __ANDROID__
                dataFile.Delete();
                dataFile.Dispose();
#elif WINDOWS_UWP
                await dataFile.DeleteAsync();
                dataFile = null;
#endif
            }
        }
#endif

        public static async Task SetLocationData(SQLiteAsyncConnection locationDB)
        {
            foreach (LocationData location in await locationDB.Table<LocationData>().ToListAsync()
                    .ConfigureAwait(false))
            {
                await WeatherManager.GetProvider(location.source)
                    .UpdateLocationData(location)
                    .ConfigureAwait(false);
            }
        }
    }
}