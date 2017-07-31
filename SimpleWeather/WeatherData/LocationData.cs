﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleWeather.WeatherData
{
    public class LocationData
    {
        public string query { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }

        public LocationData()
        {

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
        }
#endif
    }
}
