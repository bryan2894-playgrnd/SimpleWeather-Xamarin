﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleWeather.WeatherData
{
    public enum LocationType
    {
        GPS = -1,
        Search
    }

    public class LocationData
#if __ANDROID__
        : Java.Lang.Object
#endif
    {
        [Newtonsoft.Json.JsonProperty]
        public string query { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double latitude { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double longitude { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public LocationType locationType { get; set; } = LocationType.Search;
        [Newtonsoft.Json.JsonProperty]
        public string source { get; set; }

        public LocationData()
        {
            source = Utils.Settings.API;
        }

        public LocationData(string query)
        {
            this.query = query;
            source = Utils.Settings.API;
        }

#if WINDOWS_UWP
        public LocationData(string query, Windows.Devices.Geolocation.Geoposition geoPos)
        {
            SetData(query, geoPos);
        }

        public void SetData(string query, Windows.Devices.Geolocation.Geoposition geoPos)
        {
            this.query = query;
            latitude = geoPos.Coordinate.Point.Position.Latitude;
            longitude = geoPos.Coordinate.Point.Position.Longitude;
            locationType = LocationType.GPS;
            source = Utils.Settings.API;
        }
#elif __ANDROID__
        public LocationData(string query, Android.Locations.Location location)
        {
            SetData(query, location);
        }

        public void SetData(string query, Android.Locations.Location location)
        {
            this.query = query;
            latitude = location.Latitude;
            longitude = location.Longitude;
            locationType = LocationType.GPS;
            source = Utils.Settings.API;
        }
#endif

#if WINDOWS_UWP
        public override bool Equals(System.Object obj)
#elif __ANDROID__
        public override bool Equals(Java.Lang.Object obj)
#endif
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                LocationData locData = (LocationData)obj;
                return (query == locData.query) && (latitude == locData.latitude) &&
                    (longitude == locData.longitude) && (locationType == locData.locationType) &&
                    (source == locData.source);
            }
        }

        public override int GetHashCode()
        {
            var hashCode = -364563956;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(query);
            hashCode = hashCode * -1521134295 + latitude.GetHashCode();
            hashCode = hashCode * -1521134295 + longitude.GetHashCode();
            hashCode = hashCode * -1521134295 + locationType.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(source);
            return hashCode;
        }
    }
}
