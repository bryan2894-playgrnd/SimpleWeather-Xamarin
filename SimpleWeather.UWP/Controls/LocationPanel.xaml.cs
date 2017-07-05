﻿using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleWeather.Controls
{
    public sealed partial class LocationPanel : UserControl
    {
        public LocationPanel()
        {
            this.InitializeComponent();
            SizeChanged += LocationPanel_SizeChanged;
        }

        private void LocationPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width >= 640)
                LocationName.Width = Double.NaN;
            else
                LocationName.Width = e.NewSize.Width - TempBox.ActualWidth - IconBox.ActualWidth;
        }
    }
}
