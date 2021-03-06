﻿using Newtonsoft.Json;
using SimpleWeather.Utils;
using SQLite;
using SQLiteNetExtensions;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SimpleWeather.WeatherData
{
    [JsonConverter(typeof(CustomJsonConverter))]
    [Table("weatherdata")]
    public class Weather
    {
        [JsonIgnore]
        public const string NA = "N/A";

        [TextBlob(nameof(locationblob))]
        public Location location { get; set; }
        [Ignore]
        // Doesn't store this in db
        // For DateTimeOffset, offset isn't stored when saving to db
        // Store as string (blob) instead
        // If db previously stored DateTimeOffset (as ticks) retrieve and set offset
        public DateTimeOffset update_time
        {
            get
            {
                if (DateTimeOffset.TryParseExact(updatetimeblob, "dd.MM.yyyy HH:mm:ss zzzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset result))
                    return result;
                else
                    return new DateTimeOffset(long.Parse(updatetimeblob), TimeSpan.Zero).ToOffset(location.tz_offset);
            }
            set { updatetimeblob = value.ToString("dd.MM.yyyy HH:mm:ss zzzz"); }
        }
        [TextBlob(nameof(forecastblob))]
        public Forecast[] forecast { get; set; }
        [TextBlob(nameof(hrforecastblob))]
        public HourlyForecast[] hr_forecast { get; set; }
        [TextBlob(nameof(txtforecastblob))]
        public TextForecast[] txt_forecast { get; set; }
        [TextBlob(nameof(conditionblob))]
        public Condition condition { get; set; }
        [TextBlob(nameof(atmosphereblob))]
        public Atmosphere atmosphere { get; set; }
        [TextBlob(nameof(astronomyblob))]
        public Astronomy astronomy { get; set; }
        [TextBlob(nameof(precipitationblob))]
        public Precipitation precipitation { get; set; }
        [JsonIgnore]
        [Ignore]
        // Just for passing along to where its needed
        public List<WeatherAlert> weather_alerts { get; set; }
        public string ttl { get; set; }
        public string source { get; set; }
        [PrimaryKey]
        public string query { get; set; }
        public string locale { get; set; }

        [JsonIgnore]
        public string locationblob { get; set; }
        [JsonIgnore]
        [Column("update_time")]
        // Keep DateTimeOffset column name to get data as string
        public string updatetimeblob { get; set; }
        [JsonIgnore]
        public string forecastblob { get; set; }
        [JsonIgnore]
        public string hrforecastblob { get; set; }
        [JsonIgnore]
        public string txtforecastblob { get; set; }
        [JsonIgnore]
        public string conditionblob { get; set; }
        [JsonIgnore]
        public string atmosphereblob { get; set; }
        [JsonIgnore]
        public string astronomyblob { get; set; }
        [JsonIgnore]
        public string precipitationblob { get; set; }

        [JsonConstructor]
        public Weather()
        {
            // Needed for deserialization
        }

        public Weather(WeatherYahoo.Rootobject root)
        {
            location = new Location(root.query);
            update_time = DateTimeOffset.ParseExact(root.query.created,
                            "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            forecast = new Forecast[root.query.results.channel.item.forecast.Length];
            for (int i = 0; i < forecast.Length; i++)
            {
                forecast[i] = new Forecast(root.query.results.channel.item.forecast[i]);
            }
            condition = new Condition(root.query.results.channel);
            atmosphere = new Atmosphere(root.query.results.channel.atmosphere);
            astronomy = new Astronomy(root.query.results.channel.astronomy);
            ttl = root.query.results.channel.ttl;

            // Set feelslike temp
            if (condition.temp_f > 80)
            {
                condition.feelslike_f = (float)WeatherUtils.CalculateHeatIndex(condition.temp_f, int.Parse(atmosphere.humidity.Replace("%", "")));
                condition.feelslike_c = float.Parse(ConversionMethods.FtoC(condition.feelslike_f.ToString()));
            }

            source = WeatherAPI.Yahoo;
        }

        public Weather(WeatherUnderground.Rootobject root)
        {
            location = new Location(root.current_observation);
            update_time = DateTimeOffset.Parse(root.current_observation.local_time_rfc822);
            forecast = new Forecast[root.forecast.simpleforecast.forecastday.Length];
            for (int i = 0; i < forecast.Length; i++)
            {
                forecast[i] = new Forecast(root.forecast.simpleforecast.forecastday[i]);

                if (i == 0)
                {
                    // Note: WUnderground API bug
                    // Data sometimes returns forecast from some date in the past
                    // If we come across this invalidate the data
                    var diffSpan = DateTime.UtcNow - forecast[i].date.ToUniversalTime();
                    if (forecast[i].date.ToUniversalTime() < DateTime.UtcNow && diffSpan.TotalDays > 2)
                        throw new WeatherException(WeatherUtils.ErrorStatus.Unknown);
                }
            }
            hr_forecast = new HourlyForecast[root.hourly_forecast.Length];
            for (int i = 0; i < hr_forecast.Length; i++)
            {
                hr_forecast[i] = new HourlyForecast(root.hourly_forecast[i]);
            }
            txt_forecast = new TextForecast[root.forecast.txt_forecast.forecastday.Length];
            for (int i = 0; i < txt_forecast.Length; i++)
            {
                txt_forecast[i] = new TextForecast(root.forecast.txt_forecast.forecastday[i]);

                // Note: WUnderground API bug
                // If array is not null and we're expecting data
                // and that data is invalid, invalidate weather data
                if (String.IsNullOrWhiteSpace(txt_forecast[i].title) &&
                    String.IsNullOrWhiteSpace(txt_forecast[i].fcttext) &&
                    String.IsNullOrWhiteSpace(txt_forecast[i].fcttext_metric))
                    throw new WeatherException(WeatherUtils.ErrorStatus.Unknown);
            }
            condition = new Condition(root.current_observation);
            atmosphere = new Atmosphere(root.current_observation);
            astronomy = new Astronomy(root.sun_phase);
            precipitation = new Precipitation(root.forecast.simpleforecast.forecastday[0]);
            ttl = "60";

            source = WeatherAPI.WeatherUnderground;
        }

        public Weather(OpenWeather.CurrentRootobject currRoot, OpenWeather.ForecastRootobject foreRoot)
        {
            location = new Location(foreRoot);
            update_time = DateTimeOffset.FromUnixTimeSeconds(currRoot.dt);

            // 5-day forecast / 3-hr forecast
            // 24hr / 3hr = 8items for each day
            forecast = new Forecast[5];
            hr_forecast = new HourlyForecast[foreRoot.list.Length];

            // Store potential min/max values
            float dayMax = float.NaN;
            float dayMin = float.NaN;
            int lastDay = 0;

            for (int i = 0; i < foreRoot.list.Length; i++)
            {
                hr_forecast[i] = new HourlyForecast(foreRoot.list[i]);

                float max = foreRoot.list[i].main.temp_max;
                if (!float.IsNaN(max) && (float.IsNaN(dayMax) || max > dayMax))
                {
                    dayMax = max;
                }

                float min = foreRoot.list[i].main.temp_min;
                if (!float.IsNaN(min) && (float.IsNaN(dayMin) || min < dayMin))
                {
                    dayMin = min;
                }

                // Get every 8th item for daily forecast
                if (i % 8 == 0)
                {
                    lastDay = i / 8;

                    forecast[i / 8] = new Forecast(foreRoot.list[i]);
                }

                // This is possibly the last forecast for the day (3-hrly forecast)
                // Set the min / max temp here and reset
                if (hr_forecast[i].date.Hour >= 21)
                {
                    if (!float.IsNaN(dayMax))
                    {
                        forecast[lastDay].high_f = ConversionMethods.KtoF(dayMax.ToString(CultureInfo.InvariantCulture));
                        forecast[lastDay].high_c = ConversionMethods.KtoC(dayMax.ToString(CultureInfo.InvariantCulture));
                    }
                    if (!float.IsNaN(dayMin))
                    {
                        forecast[lastDay].low_f = ConversionMethods.KtoF(dayMin.ToString(CultureInfo.InvariantCulture));
                        forecast[lastDay].low_c = ConversionMethods.KtoC(dayMin.ToString(CultureInfo.InvariantCulture));
                    }

                    dayMax = float.NaN;
                    dayMin = float.NaN;
                }
            }
            condition = new Condition(currRoot);
            atmosphere = new Atmosphere(currRoot);
            astronomy = new Astronomy(currRoot);
            precipitation = new Precipitation(currRoot);
            ttl = "120";

            query = currRoot.id.ToString();

            // Set feelslike temp
            condition.feelslike_f = float.Parse(WeatherUtils.GetFeelsLikeTemp(condition.temp_f.ToString(), condition.wind_mph.ToString(), atmosphere.humidity));
            condition.feelslike_c = float.Parse(ConversionMethods.FtoC(condition.feelslike_f.ToString()));

            source = WeatherAPI.OpenWeatherMap;
        }

        public Weather(Metno.weatherdata foreRoot, Metno.astrodata astroRoot)
        {
            location = new Location(foreRoot);
            update_time = foreRoot.created;

            // 9-day forecast / hrly -> 6hrly forecast
            var forecastL = new List<Forecast>();
            var hr_forecastL = new List<HourlyForecast>();

            // Store potential min/max values
            float dayMax = float.NaN;
            float dayMin = float.NaN;

            // Flag values
            bool end = false;
            bool conditionSet = false;
            int fcastCount = 0;

            DateTime startDate = foreRoot.meta.First().from;
            DateTime endDate = foreRoot.meta.Last().to.Subtract(new TimeSpan(6, 0, 0));
            Forecast fcast = null;

            // Metno data is troublesome to parse thru
            for (int i = 0; i < foreRoot.product.time.Length; i++)
            {
                var time = foreRoot.product.time[i];
                DateTime date = time.from;

                // Create condition for next 2hrs from data
                if (i == 0 && date.Equals(startDate))
                {
                    condition = new Condition(time);
                    atmosphere = new Atmosphere(time);
                    precipitation = new Precipitation(time);
                }

                // This contains all weather details
                if (!end && time.to.Subtract(time.from).Ticks == 0)
                {
                    // Find max/min for each hour
                    float temp = (float)time.location.temperature.value;
                    if (!float.IsNaN(temp) && (float.IsNaN(dayMax) || temp > dayMax))
                    {
                        dayMax = temp;
                    }
                    if (!float.IsNaN(temp) && (float.IsNaN(dayMin) || temp < dayMin))
                    {
                        dayMin = temp;
                    }

                    // Add a new hour
                    hr_forecastL.Add(new HourlyForecast(time));

                    // Create new forecast
                    if (date.Hour == 0 || date.Equals(startDate))
                    {
                        fcastCount++;

                        // Oops, we missed one
                        if (fcast != null && fcastCount != forecastL.Count)
                        {
                            // Set forecast properties here:
                            // condition (set in provider GetWeather method)
                            // date
                            fcast.date = date;
                            // high
                            fcast.high_f = ConversionMethods.CtoF(dayMax.ToString(CultureInfo.InvariantCulture));
                            fcast.high_c = Math.Round(dayMax).ToString();
                            // low
                            fcast.low_f = ConversionMethods.CtoF(dayMin.ToString(CultureInfo.InvariantCulture));
                            fcast.low_c = Math.Round(dayMin).ToString();
                            // icon
                            forecastL.Add(fcast);

                            // Reset
                            dayMax = float.NaN;
                            dayMin = float.NaN;
                        }

                        fcast = new Forecast(time);
                    }
                    // Last forecast for day; create forecast
                    if (date.Hour == 23 || date.Equals(endDate))
                    {
                        // condition (set in provider GetWeather method)
                        // date
                        fcast.date = date;
                        // high
                        fcast.high_f = ConversionMethods.CtoF(dayMax.ToString(CultureInfo.InvariantCulture));
                        fcast.high_c = Math.Round(dayMax).ToString();
                        // low
                        fcast.low_f = ConversionMethods.CtoF(dayMin.ToString(CultureInfo.InvariantCulture));
                        fcast.low_c = Math.Round(dayMin).ToString();
                        // icon
                        forecastL.Add(fcast);

                        if (date.Equals(endDate))
                            end = true;

                        // Reset
                        dayMax = float.NaN;
                        dayMin = float.NaN;
                        fcast = null;
                    }
                }

                // Get conditions for hour if available
                if (hr_forecastL.Count > 1 &&
                    hr_forecastL[hr_forecastL.Count - 2].date.Equals(time.from))
                {
                    // Set condition from id
                    var hr = hr_forecastL[hr_forecastL.Count - 2];
                    if (String.IsNullOrEmpty(hr.icon))
                    {
                        if (time.location.symbol != null)
                        {
                            hr.condition = time.location.symbol.id;
                            hr.icon = time.location.symbol.number.ToString();
                        }
                    }
                }
                else if (end && hr_forecastL.Last().date.Equals(time.from))
                {
                    // Set condition from id
                    var hr = hr_forecastL.Last();
                    if (String.IsNullOrEmpty(hr.icon))
                    {
                        if (time.location.symbol != null)
                        {
                            hr.condition = time.location.symbol.id;
                            hr.icon = time.location.symbol.number.ToString();
                        }
                    }
                }

                if (fcast != null && fcast.date.Equals(time.from) && time.to.Subtract(time.from).TotalHours >= 1)
                {
                    if (time.location.symbol != null)
                    {
                        fcast.condition = time.location.symbol.id;
                        fcast.icon = time.location.symbol.number.ToString();
                    }
                }
                else if (forecastL.Count > 0 && forecastL.Last().date.Equals(time.from) && time.to.Subtract(time.from).TotalHours >= 1)
                {
                    if (String.IsNullOrEmpty(forecastL.Last().icon))
                    {
                        if (time.location.symbol != null)
                        {
                            forecastL.Last().condition = time.location.symbol.id;
                            forecastL.Last().icon = time.location.symbol.number.ToString();
                        }
                    }
                }

                if (!conditionSet && condition != null && date.Equals(startDate) && time.to.Subtract(time.from).TotalHours >= 2)
                {
                    // Set condition from id
                    if (time.location.symbol != null)
                    {
                        condition.icon = time.location.symbol.number.ToString();
                        condition.weather = time.location.symbol.id;
                    }

                    conditionSet = true;
                }
            }

            forecast = forecastL.ToArray();
            hr_forecast = hr_forecastL.ToArray();
            astronomy = new Astronomy(astroRoot);
            ttl = "120";

            query = string.Format("lat={0}&lon={1}", location.latitude, location.longitude);

            // Set feelslike temp
            condition.feelslike_f = float.Parse(WeatherUtils.GetFeelsLikeTemp(condition.temp_f.ToString(), condition.wind_mph.ToString(), atmosphere.humidity));
            condition.feelslike_c = float.Parse(ConversionMethods.FtoC(condition.feelslike_f.ToString()));

            source = WeatherAPI.MetNo;
        }

        public Weather(HERE.Rootobject root)
        {
            var now = root.feedCreation;

            location = new Location(root.observations.location[0]);
            update_time = root.feedCreation;
            forecast = new Forecast[root.dailyForecasts.forecastLocation.forecast.Length];
            for (int i = 0; i < forecast.Length; i++)
            {
                forecast[i] = new Forecast(root.dailyForecasts.forecastLocation.forecast[i]);
            }
            var tmp_hr_forecast = new List<HourlyForecast>(root.hourlyForecasts.forecastLocation.forecast.Length);
            foreach (HERE.Forecast1 forecast1 in root.hourlyForecasts.forecastLocation.forecast)
            {
                if (forecast1.utcTime.UtcDateTime < now.UtcDateTime)
                    continue;

                tmp_hr_forecast.Add(new HourlyForecast(forecast1));
            }
            hr_forecast = tmp_hr_forecast.ToArray();
            txt_forecast = new TextForecast[root.dailyForecasts.forecastLocation.forecast.Length];
            for (int i = 0; i < txt_forecast.Length; i++)
            {
                txt_forecast[i] = new TextForecast(root.dailyForecasts.forecastLocation.forecast[i]);
            }
            condition = new Condition(root.observations.location[0].observation[0]);
            atmosphere = new Atmosphere(root.observations.location[0].observation[0]);
            astronomy = new Astronomy(root.astronomy.astronomy);
            precipitation = new Precipitation(root.dailyForecasts.forecastLocation.forecast[0]);
            ttl = "180";

            source = WeatherAPI.Here;
        }

        public static Weather FromJson(JsonReader reader)
        {
            Weather obj = null;

            try
            {
                obj = new Weather();

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(location):
                            obj.location = Location.FromJson(reader);
                            break;
                        case nameof(update_time):
                            bool parsed = DateTimeOffset.TryParseExact(reader.Value.ToString(), "dd.MM.yyyy HH:mm:ss zzzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset result);
                            if (!parsed) // If we can't parse as DateTimeOffset try DateTime (data could be old)
                                result = DateTime.Parse(reader.Value.ToString());
                            else
                            {
                                // DateTimeOffset date stored in SQLite.NET doesn't store offset
                                // Try to convert to location's timezone if possible or if time is in UTC
                                if (obj.location?.tz_offset != null && result.Offset.Ticks == 0)
                                    result = result.ToOffset(obj.location.tz_offset);
                            }
                            obj.update_time = result;
                            break;
                        case nameof(forecast):
                            var forecasts = new List<Forecast>();
                            while(reader.Read() && reader.TokenType != JsonToken.EndArray)
                            {
                                if (reader.TokenType == JsonToken.String)
                                    forecasts.Add(Forecast.FromJson(reader));
                            }
                            obj.forecast = forecasts.ToArray();
                            break;
                        case nameof(hr_forecast):
                            var hr_forecasts = new List<HourlyForecast>();
                            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                            {
                                if (reader.TokenType == JsonToken.String)
                                    hr_forecasts.Add(HourlyForecast.FromJson(reader));
                            }
                            obj.hr_forecast = hr_forecasts.ToArray();
                            break;
                        case nameof(txt_forecast):
                            var txt_forecasts = new List<TextForecast>();
                            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                            {
                                if (reader.TokenType == JsonToken.String)
                                    txt_forecasts.Add(TextForecast.FromJson(reader));
                            }
                            obj.txt_forecast = txt_forecasts.ToArray();
                            break;
                        case nameof(condition):
                            obj.condition = Condition.FromJson(reader);
                            break;
                        case nameof(atmosphere):
                            obj.atmosphere = Atmosphere.FromJson(reader);
                            break;
                        case nameof(astronomy):
                            obj.astronomy = Astronomy.FromJson(reader);
                            break;
                        case nameof(precipitation):
                            obj.precipitation = Precipitation.FromJson(reader);
                            break;
                        case nameof(ttl):
                            obj.ttl = reader.Value?.ToString();
                            break;
                        case nameof(source):
                            obj.source = reader.Value?.ToString();
                            break;
                        case nameof(query):
                            obj.query = reader.Value?.ToString();
                            break;
                        case nameof(locale):
                            obj.locale = reader.Value?.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "location" : ""
                writer.WritePropertyName(nameof(location));
                writer.WriteValue(location?.ToJson());

                // "update_time" : ""
                writer.WritePropertyName(nameof(update_time));
                writer.WriteValue(update_time.ToString("dd.MM.yyyy HH:mm:ss zzzz"));

                // "forecast" : ""
                if (forecast != null)
                {
                    writer.WritePropertyName(nameof(forecast));
                    writer.WriteStartArray();
                    foreach (Forecast cast in forecast)
                    {
                        writer.WriteValue(cast?.ToJson());
                    }
                    writer.WriteEndArray();
                }

                // "hr_forecast" : ""
                if (hr_forecast != null)
                {
                    writer.WritePropertyName(nameof(hr_forecast));
                    writer.WriteStartArray();
                    foreach (HourlyForecast hr_cast in hr_forecast)
                    {
                        writer.WriteValue(hr_cast?.ToJson());
                    }
                    writer.WriteEndArray();
                }

                // "txt_forecast" : ""
                if (txt_forecast != null)
                {
                    writer.WritePropertyName(nameof(txt_forecast));
                    writer.WriteStartArray();
                    foreach (TextForecast txt_cast in txt_forecast)
                    {
                        writer.WriteValue(txt_cast?.ToJson());
                    }
                    writer.WriteEndArray();
                }

                // "condition" : ""
                writer.WritePropertyName(nameof(condition));
                writer.WriteValue(condition?.ToJson());

                // "atmosphere" : ""
                writer.WritePropertyName(nameof(atmosphere));
                writer.WriteValue(atmosphere?.ToJson());

                // "astronomy" : ""
                writer.WritePropertyName(nameof(astronomy));
                writer.WriteValue(astronomy?.ToJson());

                // "precipitation" : ""
                if (precipitation != null)
                {
                    writer.WritePropertyName(nameof(precipitation));
                    writer.WriteValue(precipitation?.ToJson());
                }

                // "ttl" : ""
                writer.WritePropertyName(nameof(ttl));
                writer.WriteValue(ttl);

                // "source" : ""
                writer.WritePropertyName(nameof(source));
                writer.WriteValue(source);

                // "query" : ""
                writer.WritePropertyName(nameof(query));
                writer.WriteValue(query);

                // "locale" : ""
                writer.WritePropertyName(nameof(locale));
                writer.WriteValue(locale);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }

        public bool IsValid()
        {
            if (location == null || (forecast == null || forecast.Length == 0)
                || condition == null || atmosphere == null || astronomy == null)
                return false;
            else
                return true;
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class Location
    {
        public string name { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
        public TimeSpan tz_offset { get; set; }
        public string tz_short { get; set; }
        public string tz_long { get; set; }

        [JsonConstructor]
        private Location()
        {
            // Needed for deserialization
        }

        public Location(WeatherUnderground.Current_Observation condition)
        {
            name = condition.display_location.full;
            latitude = condition.display_location.latitude;
            longitude = condition.display_location.longitude;
            if (condition.local_tz_offset.StartsWith("-"))
                tz_offset = -TimeSpan.ParseExact(condition.local_tz_offset, "\\-hhmm", null);
            else
                tz_offset = TimeSpan.ParseExact(condition.local_tz_offset, "\\+hhmm", null);
            tz_short = condition.local_tz_short;
            tz_long = condition.local_tz_long;
        }

        public Location(WeatherYahoo.Query query)
        {
            name = query.results.channel.location.city + "," + query.results.channel.location.region;
            latitude = query.results.channel.item.lat;
            longitude = query.results.channel.item._long;
            saveTimeZone(query);
        }

        public Location(OpenWeather.ForecastRootobject root)
        {
            // Use location name from location provider
            name = null;
            latitude = root.city.coord.lat.ToString();
            longitude = root.city.coord.lon.ToString();
            tz_offset = TimeSpan.Zero;
            tz_short = "UTC";
        }

        public Location(Metno.weatherdata foreRoot)
        {
            // API doesn't provide location name (at all)
            name = null;
            latitude = foreRoot.product.time.First().location.latitude.ToString();
            longitude = foreRoot.product.time.First().location.longitude.ToString();
            tz_offset = TimeSpan.Zero;
            tz_short = "UTC";
        }

        public Location(HERE.Location location)
        {
            // Use location name from location provider
            name = null;
            latitude = location.latitude.ToString();
            longitude = location.longitude.ToString();
            tz_offset = TimeSpan.Zero;
            tz_short = "UTC";
        }

        private void saveTimeZone(WeatherYahoo.Query query)
        {
            /* Get TimeZone info by using UTC and local build time */
            // Now
            DateTime utc = DateTime.ParseExact(query.created,
                            "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

            // There
            int AMPMidx = query.results.channel.lastBuildDate.LastIndexOf(" AM ");
            if (AMPMidx < 0)
                AMPMidx = query.results.channel.lastBuildDate.LastIndexOf(" PM ");

            DateTime there = DateTime.ParseExact(query.results.channel.lastBuildDate.Substring(0, AMPMidx + 4),
                "ddd, dd MMM yyyy hh:mm tt ", null);
            TimeSpan offset = there - utc;

            tz_offset = TimeSpan.Parse(string.Format("{0}:{1}", offset.Hours, offset.Minutes));
            tz_short = query.results.channel.lastBuildDate.Substring(AMPMidx + 4);
        }

        public static Location FromJson(JsonReader extReader)
        {
            Location obj = null;

            try
            {
                obj = new Location();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(name):
                            obj.name = reader.Value.ToString();
                            break;
                        case nameof(latitude):
                            obj.latitude = reader.Value.ToString();
                            break;
                        case nameof(longitude):
                            obj.longitude = reader.Value.ToString();
                            break;
                        case nameof(tz_offset):
                            obj.tz_offset = TimeSpan.Parse(reader.Value.ToString());
                            break;
                        case nameof(tz_short):
                            obj.tz_short = reader.Value.ToString();
                            break;
                        case nameof(tz_long):
                            obj.tz_long = reader.Value?.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "name" : ""
                writer.WritePropertyName(nameof(name));
                writer.WriteValue(name);

                // "latitude" : ""
                writer.WritePropertyName(nameof(latitude));
                writer.WriteValue(latitude);

                // "longitude" : ""
                writer.WritePropertyName(nameof(longitude));
                writer.WriteValue(longitude);

                // "tz_offset" : ""
                writer.WritePropertyName(nameof(tz_offset));
                writer.WriteValue(tz_offset);

                // "tz_short" : ""
                writer.WritePropertyName(nameof(tz_short));
                writer.WriteValue(tz_short);

                // "tz_long" : ""
                writer.WritePropertyName(nameof(tz_long));
                writer.WriteValue(tz_long);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class Forecast
    {
        public DateTime date { get; set; }
        public string high_f { get; set; }
        public string high_c { get; set; }
        public string low_f { get; set; }
        public string low_c { get; set; }
        public string condition { get; set; }
        public string icon { get; set; }

        [JsonConstructor]
        private Forecast()
        {
            // Needed for deserialization
        }

        public Forecast(WeatherYahoo.Forecast forecast)
        {
            date = DateTime.Parse(forecast.date,
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            high_f = forecast.high;
            high_c = ConversionMethods.FtoC(high_f);
            low_f = forecast.low;
            low_c = ConversionMethods.FtoC(low_f);
            condition = forecast.text;
            icon = WeatherManager.GetProvider(WeatherAPI.Yahoo)
                   .GetWeatherIcon(forecast.code);
        }

        public Forecast(WeatherUnderground.Forecastday1 forecast)
        {
            date = ConversionMethods.ToEpochDateTime(forecast.date.epoch).ToLocalTime();
            high_f = forecast.high.fahrenheit;
            high_c = forecast.high.celsius;
            low_f = forecast.low.fahrenheit;
            low_c = forecast.low.celsius;
            condition = forecast.conditions;
            icon = WeatherManager.GetProvider(WeatherAPI.WeatherUnderground)
                   .GetWeatherIcon(forecast.icon_url.Replace("http://icons.wxug.com/i/c/k/", "").Replace(".gif", ""));
        }

        public Forecast(OpenWeather.List forecast)
        {
            date = DateTimeOffset.FromUnixTimeSeconds(forecast.dt).DateTime;
            high_f = ConversionMethods.KtoF(forecast.main.temp_max.ToString(CultureInfo.InvariantCulture));
            high_c = ConversionMethods.KtoC(forecast.main.temp_max.ToString(CultureInfo.InvariantCulture));
            low_f = ConversionMethods.KtoF(forecast.main.temp_min.ToString(CultureInfo.InvariantCulture));
            low_c = ConversionMethods.KtoC(forecast.main.temp_min.ToString(CultureInfo.InvariantCulture));
            condition = forecast.weather[0].description.ToUpperCase();
            icon = WeatherManager.GetProvider(WeatherAPI.OpenWeatherMap)
                   .GetWeatherIcon(forecast.weather[0].id.ToString());
        }

        public Forecast(Metno.weatherdataProductTime time)
        {
            date = time.from;
            // Don't bother setting other values; they're not available yet
        }

        public Forecast(HERE.Forecast forecast)
        {
            date = forecast.utcTime.UtcDateTime;
            high_f = forecast.highTemperature;
            high_c = ConversionMethods.FtoC(forecast.highTemperature);
            low_f = forecast.lowTemperature;
            low_c = ConversionMethods.FtoC(forecast.lowTemperature);
            condition = forecast.description.ToPascalCase();
            icon = WeatherManager.GetProvider(WeatherAPI.Here)
                   .GetWeatherIcon(string.Format("{0}_{1}", forecast.daylight, forecast.iconName));
        }

        public static Forecast FromJson(JsonReader extReader)
        {
            Forecast obj = null;

            try
            {
                obj = new Forecast();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(date):
                            obj.date = DateTime.Parse(reader.Value.ToString());
                            break;
                        case nameof(high_f):
                            obj.high_f = reader.Value.ToString();
                            break;
                        case nameof(high_c):
                            obj.high_c = reader.Value.ToString();
                            break;
                        case nameof(low_f):
                            obj.low_f = reader.Value.ToString();
                            break;
                        case nameof(low_c):
                            obj.low_c = reader.Value.ToString();
                            break;
                        case nameof(condition):
                            obj.condition = reader.Value.ToString();
                            break;
                        case nameof(icon):
                            obj.icon = reader.Value.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "date" : ""
                writer.WritePropertyName(nameof(date));
                writer.WriteValue(date);

                // "high_f" : ""
                writer.WritePropertyName(nameof(high_f));
                writer.WriteValue(high_f);

                // "high_c" : ""
                writer.WritePropertyName(nameof(high_c));
                writer.WriteValue(high_c);

                // "low_f" : ""
                writer.WritePropertyName(nameof(low_f));
                writer.WriteValue(low_f);

                // "low_c" : ""
                writer.WritePropertyName(nameof(low_c));
                writer.WriteValue(low_c);

                // "condition" : ""
                writer.WritePropertyName(nameof(condition));
                writer.WriteValue(condition);

                // "icon" : ""
                writer.WritePropertyName(nameof(icon));
                writer.WriteValue(icon);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class HourlyForecast
    {
        [JsonIgnore]
        public DateTimeOffset date
        {
            get
            {
                if (DateTimeOffset.TryParseExact(_date, "dd.MM.yyyy HH:mm:ss zzzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset result))
                    return result;
                else
                    return DateTimeOffset.Parse(_date);
            }
            set { _date = value.ToString("dd.MM.yyyy HH:mm:ss zzzz"); }
        }
        public string high_f { get; set; }
        public string high_c { get; set; }
        public string condition { get; set; }
        public string icon { get; set; }
        public string pop { get; set; }
        public int wind_degrees { get; set; }
        public float wind_mph { get; set; }
        public float wind_kph { get; set; }

        [JsonProperty(PropertyName = nameof(date))]
        private string _date { get; set; }

        [JsonConstructor]
        private HourlyForecast()
        {
            // Needed for deserialization
        }

        public HourlyForecast(WeatherUnderground.Hourly_Forecast hr_forecast)
        {
            var dateformat = string.Format("{0}/{1}/{2} {3}", hr_forecast.FCTTIME.mon, hr_forecast.FCTTIME.mday, hr_forecast.FCTTIME.year, hr_forecast.FCTTIME.civil);
            date = DateTimeOffset.Parse(dateformat, CultureInfo.InvariantCulture);
            high_f = hr_forecast.temp.english;
            high_c = hr_forecast.temp.metric;
            condition = hr_forecast.condition;

            icon = WeatherManager.GetProvider(WeatherAPI.WeatherUnderground)
                   .GetWeatherIcon(hr_forecast.icon_url.Replace("http://icons.wxug.com/i/c/k/", "").Replace(".gif", ""));

            pop = hr_forecast.pop;
            wind_degrees = int.Parse(hr_forecast.wdir.degrees);
            wind_mph = float.Parse(hr_forecast.wspd.english);
            wind_kph = float.Parse(hr_forecast.wspd.metric);
        }

        public HourlyForecast(OpenWeather.List hr_forecast)
        {
            date = DateTimeOffset.FromUnixTimeSeconds(hr_forecast.dt);
            high_f = ConversionMethods.KtoF(hr_forecast.main.temp.ToString(CultureInfo.InvariantCulture));
            high_c = ConversionMethods.KtoC(hr_forecast.main.temp.ToString(CultureInfo.InvariantCulture));
            condition = hr_forecast.weather[0].description.ToUpperCase();

            // Use icon to determine if day or night
            string ico = hr_forecast.weather[0].icon;
            string dn = ico.Last().ToString();

            if (int.TryParse(dn, out int x))
                dn = String.Empty;

            icon = WeatherManager.GetProvider(WeatherAPI.OpenWeatherMap)
                   .GetWeatherIcon(hr_forecast.weather[0].id.ToString() + dn);

            // Use cloudiness value here
            pop = hr_forecast.clouds.all.ToString();
            wind_degrees = (int)hr_forecast.wind.deg;
            wind_mph = (float)Math.Round(double.Parse(ConversionMethods.MSecToMph(hr_forecast.wind.speed.ToString())));
            wind_kph = (float)Math.Round(double.Parse(ConversionMethods.MSecToKph(hr_forecast.wind.speed.ToString())));
        }

        public HourlyForecast(Metno.weatherdataProductTime hr_forecast)
        {
            date = new DateTimeOffset(hr_forecast.from, TimeSpan.Zero);
            high_f = ConversionMethods.CtoF(hr_forecast.location.temperature.value.ToString(CultureInfo.InvariantCulture));
            high_c = hr_forecast.location.temperature.value.ToString();
            //condition = hr_forecast.weather[0].main;
            //icon = hr_forecast.weather[0].id.ToString();
            // Use cloudiness value here
            pop = ((int)Math.Round(hr_forecast.location.cloudiness.percent)).ToString();
            wind_degrees = (int)Math.Round(hr_forecast.location.windDirection.deg);
            wind_mph = (float)Math.Round(double.Parse(ConversionMethods.MSecToMph(hr_forecast.location.windSpeed.mps.ToString())));
            wind_kph = (float)Math.Round(double.Parse(ConversionMethods.MSecToKph(hr_forecast.location.windSpeed.mps.ToString())));
        }

        public HourlyForecast(HERE.Forecast1 hr_forecast)
        {
            date = hr_forecast.utcTime;
            high_f = hr_forecast.temperature;
            high_c = ConversionMethods.FtoC(hr_forecast.temperature);
            condition = hr_forecast.description.ToPascalCase();

            icon = WeatherManager.GetProvider(WeatherAPI.Here)
                   .GetWeatherIcon(string.Format("{0}_{1}", hr_forecast.daylight, hr_forecast.iconName));

            pop = hr_forecast.precipitationProbability;
            wind_degrees = int.Parse(hr_forecast.windDirection);
            wind_mph = float.Parse(hr_forecast.windSpeed);
            wind_kph = float.Parse(ConversionMethods.MphToKph(hr_forecast.windSpeed));
        }

        public static HourlyForecast FromJson(JsonReader extReader)
        {
            HourlyForecast obj = null;

            try
            {
                obj = new HourlyForecast();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(date):
                            obj._date = reader.Value.ToString();
                            break;
                        case nameof(high_f):
                            obj.high_f = reader.Value.ToString();
                            break;
                        case nameof(high_c):
                            obj.high_c = reader.Value.ToString();
                            break;
                        case nameof(condition):
                            obj.condition = reader.Value.ToString();
                            break;
                        case nameof(icon):
                            obj.icon = reader.Value.ToString();
                            break;
                        case nameof(pop):
                            obj.pop = reader.Value.ToString();
                            break;
                        case nameof(wind_degrees):
                            obj.wind_degrees = int.Parse(reader.Value.ToString());
                            break;
                        case nameof(wind_mph):
                            obj.wind_mph = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(wind_kph):
                            obj.wind_kph = float.Parse(reader.Value.ToString());
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "date" : ""
                writer.WritePropertyName(nameof(date));
                writer.WriteValue(_date);

                // "high_f" : ""
                writer.WritePropertyName(nameof(high_f));
                writer.WriteValue(high_f);

                // "high_c" : ""
                writer.WritePropertyName(nameof(high_c));
                writer.WriteValue(high_c);

                // "condition" : ""
                writer.WritePropertyName(nameof(condition));
                writer.WriteValue(condition);

                // "icon" : ""
                writer.WritePropertyName(nameof(icon));
                writer.WriteValue(icon);

                // "pop" : ""
                writer.WritePropertyName(nameof(pop));
                writer.WriteValue(pop);

                // "wind_degrees" : ""
                writer.WritePropertyName(nameof(wind_degrees));
                writer.WriteValue(wind_degrees);

                // "wind_mph" : ""
                writer.WritePropertyName(nameof(wind_mph));
                writer.WriteValue(wind_mph);

                // "wind_kph" : ""
                writer.WritePropertyName(nameof(wind_kph));
                writer.WriteValue(wind_kph);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class TextForecast
    {
        public string title { get; set; }
        public string fcttext { get; set; }
        public string fcttext_metric { get; set; }
        public string icon { get; set; }
        public string pop { get; set; }

        [JsonConstructor]
        private TextForecast()
        {
            // Needed for deserialization
        }

        public TextForecast(WeatherUnderground.Forecastday txt_forecast)
        {
            title = txt_forecast.title;
            fcttext = txt_forecast.fcttext;
            fcttext_metric = txt_forecast.fcttext_metric;

            icon = WeatherManager.GetProvider(WeatherAPI.WeatherUnderground)
                   .GetWeatherIcon(txt_forecast.icon_url.Replace("http://icons.wxug.com/i/c/k/", "").Replace(".gif", ""));

            pop = txt_forecast.pop;
        }

        public TextForecast(HERE.Forecast forecast)
        {
            title = forecast.weekday;

            String fctxt = String.Format("{0} {1} {2}: {3}",
                forecast.description.ToPascalCase(), forecast.beaufortDescription.ToPascalCase(),
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_Humidity/Text"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_humidity),
#else
                "Humidity",
#endif
                forecast.humidity + "%");

            fcttext = String.Format("{0} {1} {2}F. {3} {4}F. {5} {6} {7}mph",
                fctxt,
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_High"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_high),
#else
                "High",
#endif
                Math.Round(double.Parse(forecast.highTemperature)),
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_Low"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_low),
#else
                "Low",
#endif
                Math.Round(double.Parse(forecast.lowTemperature)),
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_Wind/Text"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_wind),
#else
                "Wind",
#endif
                forecast.windDesc, Math.Round(double.Parse(forecast.windSpeed)));

            fcttext_metric = String.Format("{0} {1} {2}C. {3} {4}C. {5} {6} {7}kph",
                fctxt,
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_High"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_high),
#else
                "High",
#endif
                ConversionMethods.FtoC(forecast.highTemperature),
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_Low"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_low),
#else
                "Low",
#endif
                ConversionMethods.FtoC(forecast.lowTemperature),
#if WINDOWS_UWP
                UWP.App.ResLoader.GetString("Label_Wind/Text"),
#elif __ANDROID__
                Android.App.Application.Context.GetString(Droid.Resource.String.label_wind),
#else
                "Wind",
#endif
                forecast.windDesc, Math.Round(double.Parse(ConversionMethods.MphToKph(forecast.windSpeed))));

            icon = WeatherManager.GetProvider(WeatherAPI.Here)
                   .GetWeatherIcon(string.Format("{0}_{1}", forecast.daylight, forecast.iconName));

            pop = forecast.precipitationProbability;
        }

        public static TextForecast FromJson(JsonReader extReader)
        {
            TextForecast obj = null;

            try
            {
                obj = new TextForecast();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(title):
                            obj.title = reader.Value.ToString();
                            break;
                        case nameof(fcttext):
                            obj.fcttext = reader.Value.ToString();
                            break;
                        case nameof(fcttext_metric):
                            obj.fcttext_metric = reader.Value.ToString();
                            break;
                        case nameof(icon):
                            obj.icon = reader.Value.ToString();
                            break;
                        case nameof(pop):
                            obj.pop = reader.Value.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "title" : ""
                writer.WritePropertyName(nameof(title));
                writer.WriteValue(title);

                // "fcttext" : ""
                writer.WritePropertyName(nameof(fcttext));
                writer.WriteValue(fcttext);

                // "fcttext_metric" : ""
                writer.WritePropertyName(nameof(fcttext_metric));
                writer.WriteValue(fcttext_metric);

                // "icon" : ""
                writer.WritePropertyName(nameof(icon));
                writer.WriteValue(icon);

                // "pop" : ""
                writer.WritePropertyName(nameof(pop));
                writer.WriteValue(pop);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class Condition
    {
        public string weather { get; set; }
        public float temp_f { get; set; }
        public float temp_c { get; set; }
        public int wind_degrees { get; set; }
        public float wind_mph { get; set; }
        public float wind_kph { get; set; }
        public float feelslike_f { get; set; }
        public float feelslike_c { get; set; }
        public string icon { get; set; }

        [JsonConstructor]
        private Condition()
        {
            // Needed for deserialization
        }

        public Condition(WeatherUnderground.Current_Observation condition)
        {
            weather = condition.weather;
            temp_f = condition.temp_f;
            temp_c = condition.temp_c;
            wind_degrees = condition.wind_degrees;
            wind_mph = condition.wind_mph;
            wind_kph = condition.wind_kph;
            feelslike_f = condition.feelslike_f;
            feelslike_c = condition.feelslike_c;
            icon = WeatherManager.GetProvider(WeatherAPI.WeatherUnderground)
                   .GetWeatherIcon(condition.icon_url.Replace("http://icons.wxug.com/i/c/k/", "").Replace(".gif", ""));
        }

        public Condition(WeatherYahoo.Channel channel)
        {
            weather = channel.item.condition.text;
            temp_f = float.Parse(channel.item.condition.temp);
            temp_c = float.Parse(ConversionMethods.FtoC(channel.item.condition.temp));
            wind_degrees = int.Parse(channel.wind.direction);
            wind_kph = float.Parse(channel.wind.speed);
            wind_mph = float.Parse(ConversionMethods.KphToMph(channel.wind.speed));
            feelslike_f = float.Parse(channel.wind.chill);
            feelslike_c = float.Parse(ConversionMethods.FtoC(channel.wind.chill));
            icon = WeatherManager.GetProvider(WeatherAPI.Yahoo)
                   .GetWeatherIcon(channel.item.condition.code);
        }

        public Condition(OpenWeather.CurrentRootobject root)
        {
            weather = root.weather[0].description.ToUpperCase();
            temp_f = float.Parse(ConversionMethods.KtoF(root.main.temp.ToString(CultureInfo.InvariantCulture)));
            temp_c = float.Parse(ConversionMethods.KtoC(root.main.temp.ToString(CultureInfo.InvariantCulture)));
            wind_degrees = (int)root.wind.deg;
            wind_mph = float.Parse(ConversionMethods.MSecToMph(root.wind.speed.ToString(CultureInfo.InvariantCulture)));
            wind_kph = float.Parse(ConversionMethods.MSecToKph(root.wind.speed.ToString(CultureInfo.InvariantCulture)));
            // This will be calculated after with formula
            feelslike_f = temp_f;
            feelslike_c = temp_c;

            string ico = root.weather[0].icon;
            string dn = ico.Last().ToString();

            if (int.TryParse(dn, out int x))
                dn = String.Empty;

            icon = WeatherManager.GetProvider(WeatherAPI.OpenWeatherMap)
                   .GetWeatherIcon(root.weather[0].id.ToString() + dn);
        }

        public Condition(Metno.weatherdataProductTime time)
        {
            // weather
            temp_f = float.Parse(ConversionMethods.CtoF(time.location.temperature.value.ToString()));
            temp_c = (float)time.location.temperature.value;
            wind_degrees = (int)Math.Round(time.location.windDirection.deg);
            wind_mph = (float)Math.Round(double.Parse(ConversionMethods.MSecToMph(time.location.windSpeed.mps.ToString())));
            wind_kph = (float)Math.Round(double.Parse(ConversionMethods.MSecToKph(time.location.windSpeed.mps.ToString())));
            // This will be calculated after with formula
            feelslike_f = temp_f;
            feelslike_c = temp_c;
            // icon
        }

        public Condition(HERE.Observation observation)
        {
            weather = observation.description.ToPascalCase();
            if (float.TryParse(observation.temperature, out float tempF))
            {
                temp_f = tempF;
                temp_c = float.Parse(ConversionMethods.FtoC(tempF.ToString()));
            }
            else
            {
                temp_f = 0.00f;
                temp_c = 0.00f;
            }

            if (int.TryParse(observation.windDirection, out int windDegrees))
                wind_degrees = windDegrees;
            else
                wind_degrees = 0;

            if (float.TryParse(observation.windSpeed, out float wind_Speed))
            {
                wind_mph = wind_Speed;
                wind_kph = float.Parse(ConversionMethods.MphToKph(observation.windSpeed));
            }
            else
            {
                wind_mph = 0.00f;
                wind_kph = 0.00f;
            }

            if (float.TryParse(observation.comfort, out float comfortTempF))
            {
                feelslike_f = comfortTempF;
                feelslike_c = float.Parse(ConversionMethods.FtoC(comfortTempF.ToString()));
            }
            else
            {
                feelslike_f = 0.00f;
                feelslike_c = 0.00f;
            }
            icon = WeatherManager.GetProvider(WeatherAPI.Here)
                   .GetWeatherIcon(string.Format("{0}_{1}", observation.daylight, observation.iconName));
        }

        public static Condition FromJson(JsonReader extReader)
        {
            Condition obj = null;

            try
            {
                obj = new Condition();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(weather):
                            obj.weather = reader.Value.ToString();
                            break;
                        case nameof(temp_f):
                            obj.temp_f = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(temp_c):
                            obj.temp_c = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(wind_degrees):
                            obj.wind_degrees = int.Parse(reader.Value.ToString());
                            break;
                        case nameof(wind_mph):
                            obj.wind_mph = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(wind_kph):
                            obj.wind_kph = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(feelslike_f):
                            obj.feelslike_f = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(feelslike_c):
                            obj.feelslike_c = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(icon):
                            obj.icon = reader.Value.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "weather" : ""
                writer.WritePropertyName(nameof(weather));
                writer.WriteValue(weather);

                // "temp_f" : ""
                writer.WritePropertyName(nameof(temp_f));
                writer.WriteValue(temp_f);

                // "temp_c" : ""
                writer.WritePropertyName(nameof(temp_c));
                writer.WriteValue(temp_c);

                // "wind_degrees" : ""
                writer.WritePropertyName(nameof(wind_degrees));
                writer.WriteValue(wind_degrees);

                // "wind_mph" : ""
                writer.WritePropertyName(nameof(wind_mph));
                writer.WriteValue(wind_mph);

                // "wind_kph" : ""
                writer.WritePropertyName(nameof(wind_kph));
                writer.WriteValue(wind_kph);

                // "feelslike_f" : ""
                writer.WritePropertyName(nameof(feelslike_f));
                writer.WriteValue(feelslike_f);

                // "feelslike_c" : ""
                writer.WritePropertyName(nameof(feelslike_c));
                writer.WriteValue(feelslike_c);

                // "icon" : ""
                writer.WritePropertyName(nameof(icon));
                writer.WriteValue(icon);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class Atmosphere
    {
        public string humidity { get; set; }
        public string pressure_mb { get; set; }
        public string pressure_in { get; set; }
        public string pressure_trend { get; set; }
        public string visibility_mi { get; set; }
        public string visibility_km { get; set; }

        [JsonConstructor]
        private Atmosphere()
        {
            // Needed for deserialization
        }

        public Atmosphere(WeatherUnderground.Current_Observation condition)
        {
            humidity = condition.relative_humidity;
            pressure_mb = condition.pressure_mb;
            pressure_in = condition.pressure_in;
            pressure_trend = condition.pressure_trend;
            visibility_mi = condition.visibility_mi;
            visibility_km = condition.visibility_km;
        }

        public Atmosphere(WeatherYahoo.Atmosphere atmosphere)
        {
            humidity = atmosphere.humidity;
            pressure_mb = atmosphere.pressure;
            pressure_in = ConversionMethods.MBToInHg(pressure_mb);
            pressure_trend = atmosphere.rising;
            visibility_km = atmosphere.visibility;
            visibility_mi = ConversionMethods.KmToMi(visibility_km);
        }

        public Atmosphere(OpenWeather.CurrentRootobject root)
        {
            humidity = root.main.humidity;
            pressure_mb = root.main.pressure.ToString(CultureInfo.InvariantCulture);
            pressure_in = ConversionMethods.MBToInHg(root.main.pressure.ToString(CultureInfo.InvariantCulture));
            pressure_trend = String.Empty;
            visibility_mi = ConversionMethods.KmToMi((root.visibility / 1000).ToString());
            visibility_km = (root.visibility / 1000).ToString();
        }

        public Atmosphere(Metno.weatherdataProductTime time)
        {
            humidity = Math.Round(time.location.humidity.value).ToString();
            pressure_mb = time.location.pressure.value.ToString(CultureInfo.InvariantCulture);
            pressure_in = ConversionMethods.MBToInHg(time.location.pressure.value.ToString(CultureInfo.InvariantCulture));
            pressure_trend = String.Empty;
            visibility_mi = Weather.NA;
            visibility_km = Weather.NA;
        }

        public Atmosphere(HERE.Observation observation)
        {
            humidity = observation.humidity;
            pressure_mb = ConversionMethods.InHgToMB(observation.barometerPressure);
            pressure_in = observation.barometerPressure;
            pressure_trend = observation.barometerTrend;
            visibility_mi = observation.visibility;

            if (float.TryParse(observation.visibility, NumberStyles.Float, CultureInfo.InvariantCulture, out float visible_mi))
                visibility_km = ConversionMethods.MiToKm(visible_mi.ToString(CultureInfo.InvariantCulture));
            else
                visibility_km = observation.visibility;
        }

        public static Atmosphere FromJson(JsonReader extReader)
        {
            Atmosphere obj = null;

            try
            {
                obj = new Atmosphere();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(humidity):
                            obj.humidity = reader.Value.ToString();
                            break;
                        case nameof(pressure_mb):
                            obj.pressure_mb = reader.Value.ToString();
                            break;
                        case nameof(pressure_in):
                            obj.pressure_in = reader.Value.ToString();
                            break;
                        case nameof(pressure_trend):
                            obj.pressure_trend = reader.Value.ToString();
                            break;
                        case nameof(visibility_mi):
                            obj.visibility_mi = reader.Value.ToString();
                            break;
                        case nameof(visibility_km):
                            obj.visibility_km = reader.Value.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "humidity" : ""
                writer.WritePropertyName(nameof(humidity));
                writer.WriteValue(humidity);

                // "pressure_mb" : ""
                writer.WritePropertyName(nameof(pressure_mb));
                writer.WriteValue(pressure_mb);

                // "pressure_in" : ""
                writer.WritePropertyName(nameof(pressure_in));
                writer.WriteValue(pressure_in);

                // "pressure_trend" : ""
                writer.WritePropertyName(nameof(pressure_trend));
                writer.WriteValue(pressure_trend);

                // "visibility_mi" : ""
                writer.WritePropertyName(nameof(visibility_mi));
                writer.WriteValue(visibility_mi);

                // "visibility_km" : ""
                writer.WritePropertyName(nameof(visibility_km));
                writer.WriteValue(visibility_km);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class Astronomy
    {
        public DateTime sunrise { get; set; }
        public DateTime sunset { get; set; }

        [JsonConstructor]
        private Astronomy()
        {
            // Needed for deserialization
        }

        public Astronomy(WeatherUnderground.Sun_Phase sun_phase)
        {
            sunset = DateTime.Parse(string.Format("{0}:{1}",
                sun_phase.sunset.hour, sun_phase.sunset.minute));
            sunrise = DateTime.Parse(string.Format("{0}:{1}",
                sun_phase.sunrise.hour, sun_phase.sunrise.minute));
        }

        public Astronomy(WeatherYahoo.Astronomy astronomy)
        {
            sunrise = DateTime.Parse(astronomy.sunrise);
            sunset = DateTime.Parse(astronomy.sunset);
        }

        public Astronomy(OpenWeather.CurrentRootobject root)
        {
            sunrise = DateTimeOffset.FromUnixTimeSeconds(root.sys.sunrise).DateTime;
            sunset = DateTimeOffset.FromUnixTimeSeconds(root.sys.sunset).DateTime;
        }

        public Astronomy(Metno.astrodata astroRoot)
        {
            sunrise = astroRoot.time.location.sun.rise;
            sunset = astroRoot.time.location.sun.set;

            // If the sun won't set/rise, set time to the future
            if ((bool)astroRoot?.time?.location?.sun?.never_rise)
            {
                sunrise = DateTime.Now.Date.AddYears(1).AddTicks(-1);
            }
            else if ((bool)astroRoot?.time?.location?.sun?.never_set)
            {
                sunset = DateTime.Now.Date.AddYears(1).AddTicks(-1);
            }
        }

        public Astronomy(HERE.Astronomy1[] astronomy)
        {
            var astroData = astronomy[0];

            sunrise = DateTime.Parse(astroData.sunrise);
            sunset = DateTime.Parse(astroData.sunset);
        }

        public static Astronomy FromJson(JsonReader extReader)
        {
            Astronomy obj = null;

            try
            {
                obj = new Astronomy();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(sunrise):
                            obj.sunrise = DateTime.Parse(reader.Value.ToString());
                            break;
                        case nameof(sunset):
                            obj.sunset = DateTime.Parse(reader.Value.ToString());
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "sunrise" : ""
                writer.WritePropertyName(nameof(sunrise));
                writer.WriteValue(sunrise);

                // "sunset" : ""
                writer.WritePropertyName(nameof(sunset));
                writer.WriteValue(sunset);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }

    [JsonConverter(typeof(CustomJsonConverter))]
    public class Precipitation
    {
        public string pop { get; set; }
        public float qpf_rain_in { get; set; }
        public float qpf_rain_mm { get; set; }
        public float qpf_snow_in { get; set; }
        public float qpf_snow_cm { get; set; }

        [JsonConstructor]
        private Precipitation()
        {
            // Needed for deserialization
        }

        public Precipitation(WeatherUnderground.Forecastday1 forecast)
        {
            pop = forecast.pop.ToString();
            qpf_rain_in = forecast.qpf_allday._in.GetValueOrDefault(0.00f);
            qpf_rain_mm = forecast.qpf_allday.mm.GetValueOrDefault();
            qpf_snow_in = forecast.snow_allday._in.GetValueOrDefault(0.00f);
            qpf_snow_cm = forecast.snow_allday.cm.GetValueOrDefault();
        }

        public Precipitation(OpenWeather.CurrentRootobject root)
        {
            // Use cloudiness value here
            pop = root.clouds.all.ToString();
            if (root.rain != null)
            {
                qpf_rain_in = float.Parse(ConversionMethods.MMToIn(root.rain._3h.ToString(CultureInfo.InvariantCulture)));
                qpf_rain_mm = root.rain._3h;
            }
            if (root.snow != null)
            {
                qpf_snow_in = float.Parse(ConversionMethods.MMToIn(root.snow._3h.ToString(CultureInfo.InvariantCulture)));
                qpf_snow_cm = root.snow._3h;
            }
        }

        public Precipitation(Metno.weatherdataProductTime time)
        {
            // Use cloudiness value here
            pop = Math.Round(time.location.cloudiness.percent).ToString();
            // The rest DNE
        }

        public Precipitation(HERE.Forecast forecast)
        {
            pop = forecast.precipitationProbability;

            if (float.TryParse(forecast.rainFall, NumberStyles.Float, CultureInfo.InvariantCulture, out float rain_in))
                qpf_rain_in = rain_in;
            else
                qpf_rain_in = 0.00f;

            qpf_rain_mm = float.Parse(ConversionMethods.InToMM(qpf_rain_in.ToString(CultureInfo.InvariantCulture)));

            if (float.TryParse(forecast.snowFall, NumberStyles.Float, CultureInfo.InvariantCulture, out float snow_in))
                qpf_snow_in = snow_in;
            else
                qpf_snow_in = 0.00f;

            qpf_snow_cm = float.Parse(ConversionMethods.InToMM(qpf_snow_in.ToString(CultureInfo.InvariantCulture))) / 10;
        }

        public static Precipitation FromJson(JsonReader extReader)
        {
            Precipitation obj = null;

            try
            {
                obj = new Precipitation();
                JsonReader reader;

                if (extReader.Value == null)
                    reader = extReader;
                else
                {
                    reader = new JsonTextReader(new System.IO.StringReader(extReader.Value.ToString()));
                    reader.Read(); // StartObject
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        reader.Read(); // StartObject

                    string property = reader.Value?.ToString();
                    reader.Read(); // prop value

                    switch (property)
                    {
                        case nameof(pop):
                            obj.pop = reader.Value.ToString();
                            break;
                        case nameof(qpf_rain_in):
                            obj.qpf_rain_in = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(qpf_rain_mm):
                            obj.qpf_rain_mm = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(qpf_snow_in):
                            obj.qpf_snow_in = float.Parse(reader.Value.ToString());
                            break;
                        case nameof(qpf_snow_cm):
                            obj.qpf_snow_cm = float.Parse(reader.Value.ToString());
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception)
            {
                obj = null;
            }

            return obj;
        }

        public string ToJson()
        {
            using (var sw = new System.IO.StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                // {
                writer.WriteStartObject();

                // "pop" : ""
                writer.WritePropertyName(nameof(pop));
                writer.WriteValue(pop);

                // "qpf_rain_in" : ""
                writer.WritePropertyName(nameof(qpf_rain_in));
                writer.WriteValue(qpf_rain_in);

                // "qpf_rain_mm" : ""
                writer.WritePropertyName(nameof(qpf_rain_mm));
                writer.WriteValue(qpf_rain_mm);

                // "qpf_snow_in" : ""
                writer.WritePropertyName(nameof(qpf_snow_in));
                writer.WriteValue(qpf_snow_in);

                // "qpf_snow_cm" : ""
                writer.WritePropertyName(nameof(qpf_snow_cm));
                writer.WriteValue(qpf_snow_cm);

                // }
                writer.WriteEndObject();

                return sw.ToString();
            }
        }
    }
}