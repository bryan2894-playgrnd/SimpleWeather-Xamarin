﻿using SimpleWeather.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleWeather.UWP.Controls
{
    public sealed partial class WeatherAlertPanel : UserControl
    {
        public WeatherAlertViewModel WeatherAlert
        {
            get { return (this.DataContext as WeatherAlertViewModel); }
        }

        public WeatherAlertPanel()
        {
            this.InitializeComponent();
            this.DataContextChanged += (sender, args) =>
            {
                this.Bindings.Update();

                AlertHeader.Background = new SolidColorBrush(GetColorFromAlertSeverity(WeatherAlert.AlertSeverity));
                AlertIcon.Source = GetAssetFromAlertType(WeatherAlert.AlertType);
            };
        }

        private string GetAssetFromAlertType(WeatherData.WeatherAlertType type)
        {
            string baseuri = "ms-appx:///Assets/WeatherIcons/png/";
            string fileIcon = string.Empty;

            switch (type)
            {
                case WeatherData.WeatherAlertType.DenseFog:
                    fileIcon = "fog.png";
                    break;
                case WeatherData.WeatherAlertType.Fire:
                    fileIcon = "fire.png";
                    break;
                case WeatherData.WeatherAlertType.FloodWarning:
                case WeatherData.WeatherAlertType.FloodWatch:
                    fileIcon = "flood.png";
                    break;
                case WeatherData.WeatherAlertType.Heat:
                    fileIcon = "hot.png";
                    break;
                case WeatherData.WeatherAlertType.HighWind:
                    fileIcon = "strong_wind.png";
                    break;
                case WeatherData.WeatherAlertType.HurricaneLocalStatement:
                case WeatherData.WeatherAlertType.HurricaneWindWarning:
                    fileIcon = "hurricane.png";
                    break;
                case WeatherData.WeatherAlertType.SevereThunderstormWarning:
                case WeatherData.WeatherAlertType.SevereThunderstormWatch:
                    fileIcon = "thunderstorm.png";
                    break;
                case WeatherData.WeatherAlertType.TornadoWarning:
                case WeatherData.WeatherAlertType.TornadoWatch:
                    fileIcon = "tornado.png";
                    break;
                case WeatherData.WeatherAlertType.Volcano:
                    fileIcon = "volcano.png";
                    break;
                case WeatherData.WeatherAlertType.WinterWeather:
                    fileIcon = "snowflake_cold.png";
                    break;
                case WeatherData.WeatherAlertType.DenseSmoke:
                    fileIcon = "smoke.png";
                    break;
                case WeatherData.WeatherAlertType.DustAdvisory:
                    fileIcon = "dust.png";
                    break;
                case WeatherData.WeatherAlertType.EarthquakeWarning:
                    fileIcon = "earthquake.png";
                    break;
                case WeatherData.WeatherAlertType.GaleWarning:
                    fileIcon = "gale_warning.png";
                    break;
                case WeatherData.WeatherAlertType.SmallCraft:
                    fileIcon = "small_craft_advisory.png";
                    break;
                case WeatherData.WeatherAlertType.StormWarning:
                    fileIcon = "storm_warning.png";
                    break;
                case WeatherData.WeatherAlertType.TsunamiWarning:
                case WeatherData.WeatherAlertType.TsunamiWatch:
                    fileIcon = "tsunami.png";
                    break;
                case WeatherData.WeatherAlertType.SevereWeather:
                case WeatherData.WeatherAlertType.SpecialWeatherAlert:
                default:
                    fileIcon = "ic_error_white.png";
                    break;
            }

            return baseuri + fileIcon;
        }

        private Color GetColorFromAlertSeverity(WeatherData.WeatherAlertSeverity severity)
        {
            Color color = Colors.Orange;

            switch (severity)
            {
                case WeatherData.WeatherAlertSeverity.Severe:
                    color = Colors.OrangeRed;
                    break;
                case WeatherData.WeatherAlertSeverity.Extreme:
                    color = Colors.Red;
                    break;
                case WeatherData.WeatherAlertSeverity.Moderate:
                default:
                    color = Colors.Orange;
                    break;
            }

            return color;
        }
    }
}
