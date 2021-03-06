﻿using System;
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Support.V4.Content;
using Android.Support.Wear.Widget.Drawer;
using Android.Support.Wearable.Activity;
using Android.Support.Wearable.Views;
using Android.Views;
using Android.Widget;
using Google.Android.Wearable.Intent;
using Java.Lang;
using SimpleWeather.Controls;
using SimpleWeather.Droid.Wear.Helpers;
using SimpleWeather.Utils;

namespace SimpleWeather.Droid.Wear
{
    [Activity(Label = "MainActivity", Theme = "@style/WearAppTheme")]
    public class MainActivity : Activity,
        IMenuItemOnMenuItemClickListener, WearableNavigationDrawerView.IOnItemSelectedListener,
        IWeatherViewLoadedListener
    {
        private WearableNavigationDrawerView mWearableNavigationDrawer;
        private WearableActionDrawerView mWearableActionDrawer;
        private NavDrawerAdapter mNavDrawerAdapter;
        private LocalBroadcastReceiver mBroadcastReceiver;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            mWearableActionDrawer = FindViewById<WearableActionDrawerView>(Resource.Id.bottom_action_drawer);
            mWearableActionDrawer.SetOnMenuItemClickListener(this);
            mWearableActionDrawer.PeekOnScrollDownEnabled = true;

            mWearableNavigationDrawer = FindViewById<WearableNavigationDrawerView>(Resource.Id.top_nav_drawer);
            mWearableNavigationDrawer.AddOnItemSelectedListener(this);
            mWearableNavigationDrawer.PeekOnScrollDownEnabled = true;
            mNavDrawerAdapter = new NavDrawerAdapter(this);
            mWearableNavigationDrawer.SetAdapter(mNavDrawerAdapter);

            mBroadcastReceiver = new LocalBroadcastReceiver();
            mBroadcastReceiver.BroadcastReceived += (context, intent) =>
            {
                if (WearableDataListenerService.ACTION_SHOWSTORELISTING.Equals(intent?.Action))
                {
                    var intentAndroid = new Intent(Intent.ActionView)
                        .AddCategory(Intent.CategoryBrowsable)
                        .SetData(WearableHelper.PlayStoreURI);

                    RemoteIntent.StartRemoteActivity(this, intentAndroid,
                        new ConfirmationResultReceiver(this));
                }
                else if (WearableDataListenerService.ACTION_OPENONPHONE.Equals(intent?.Action))
                {
                    bool success = (bool)intent?.GetBooleanExtra(WearableDataListenerService.EXTRA_SUCCESS, false);

                    new ConfirmationOverlay()
                        .SetType(success ? ConfirmationOverlay.OpenOnPhoneAnimation : ConfirmationOverlay.FailureAnimation)
                        .ShowOn(this);
                }
            };
            var filter = new IntentFilter();
            filter.AddAction(WearableDataListenerService.ACTION_SHOWSTORELISTING);
            filter.AddAction(WearableDataListenerService.ACTION_OPENONPHONE);
            LocalBroadcastManager.GetInstance(this).RegisterReceiver(mBroadcastReceiver, filter);

            // Create your application here
            Fragment fragment = FragmentManager.FindFragmentById(Resource.Id.fragment_container);

            // Check if fragment exists
            if (fragment == null)
            {
                fragment = new WeatherNowFragment();

                // Navigate to WeatherNowFragment
                FragmentManager.BeginTransaction()
                    .Replace(Resource.Id.fragment_container, fragment, "home")
                    .Commit();
            }
        }

        public override void OnBackPressed()
        {
            Fragment current = FragmentManager.FindFragmentById(Resource.Id.fragment_container);
            // Destroy untagged fragments onbackpressed
            if (current != null)
            {
                FragmentManager.BeginTransaction()
                    .Remove(current)
                    .Commit();
                current.OnDestroy();
                current = null;

                // Reset to home
                mWearableNavigationDrawer.SetCurrentItem(0, false);
            }
            else
                base.OnBackPressed();
        }

        public bool OnMenuItemClick(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menu_changelocation:
                    StartActivity(new Intent(this, typeof(SetupActivity)));
                    break;
                case Resource.Id.menu_settings:
                    StartActivity(new Intent(this, typeof(SettingsActivity)));
                    break;
                case Resource.Id.menu_openonphone:
                    StartService(new Intent(this, typeof(WearableDataListenerService))
                        .SetAction(WearableDataListenerService.ACTION_OPENONPHONE));
                    break;
            }

            return true;
        }

        public void OnItemSelected(int position)
        {
            Fragment current = FragmentManager.FindFragmentById(Resource.Id.fragment_container);
            Type targetFragmentType = null;
            WeatherListType weatherListType = 0;

            switch (mNavDrawerAdapter?.GetStringId(position))
            {
                case Resource.String.label_condition:
                default:
                    targetFragmentType = typeof(WeatherNowFragment);
                    break;
                case Resource.String.title_fragment_alerts:
                    targetFragmentType = typeof(WeatherListFragment);
                    weatherListType = WeatherListType.Alerts;
                    break;
                case Resource.String.label_forecast:
                    targetFragmentType = typeof(WeatherListFragment);
                    weatherListType = WeatherListType.Forecast;
                    break;
                case Resource.String.label_hourlyforecast:
                    targetFragmentType = typeof(WeatherListFragment);
                    weatherListType = WeatherListType.HourlyForecast;
                    break;
                case Resource.String.label_details:
                    targetFragmentType = typeof(WeatherDetailsFragment);
                    break;
            }

            if (typeof(WeatherNowFragment).Equals(targetFragmentType))
            {
                if (!Class.FromType(typeof(WeatherNowFragment)).Equals(current.Class))
                {
                    // Pop all since we're going home
                    FragmentManager.PopBackStackImmediate(null, PopBackStackFlags.Inclusive);
                }
            }
            else if (typeof(WeatherListFragment).Equals(targetFragmentType))
            {
                if (!Class.FromType(targetFragmentType).Equals(current.Class))
                {
                    // Add fragment to backstack
                    var ft = FragmentManager.BeginTransaction();
                    ft.Add(Resource.Id.fragment_container,
                           WeatherListFragment.NewInstance(weatherListType, mNavDrawerAdapter.WeatherNowView),
                           null)
                      .AddToBackStack(null);

                    if (FragmentManager.BackStackEntryCount > 0)
                        ft.Remove(current);

                    ft.Commit();
                }
                else if (current is WeatherListFragment forecastFragment)
                {
                    if (forecastFragment.Arguments != null &&
                        ((WeatherListType)forecastFragment.Arguments.GetInt("WeatherListType", 0)) != weatherListType)
                    {
                        Bundle args = new Bundle();
                        args.PutInt("WeatherListType", (int)weatherListType);
                        forecastFragment.Arguments = args;
                        forecastFragment.Initialize();
                    }
                }
            }
            else if (typeof(WeatherDetailsFragment).Equals(targetFragmentType))
            {
                if (!Class.FromType(typeof(WeatherDetailsFragment)).Equals(current.Class))
                {
                    // Add fragment to backstack
                    var ft = FragmentManager.BeginTransaction();
                    ft.Add(Resource.Id.fragment_container, WeatherDetailsFragment.NewInstance(mNavDrawerAdapter.WeatherNowView), null)
                      .AddToBackStack(null);

                    if (FragmentManager.BackStackEntryCount > 0)
                        ft.Remove(current);

                    ft.Commit();
                }
            }
        }

        protected override void OnDestroy()
        {
            LocalBroadcastManager.GetInstance(this).UnregisterReceiver(mBroadcastReceiver);
            base.OnDestroy();
        }

        public void OnWeatherViewUpdated(WeatherNowViewModel weatherNowView)
        {
            mNavDrawerAdapter.UpdateNavDrawerItems(weatherNowView);
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (mWearableActionDrawer != null)
            {
                var menuitem = mWearableActionDrawer.Menu?.FindItem(Resource.Id.menu_changelocation);

                if (Settings.DataSync != WearableDataSync.Off && menuitem != null)
                {
                    // remove change location if exists
                    mWearableActionDrawer.Menu.RemoveItem(Resource.Id.menu_changelocation);
                }
                else if (Settings.DataSync == WearableDataSync.Off && menuitem == null)
                {
                    // restore all menu options
                    mWearableActionDrawer.Menu.Clear();
                    MenuInflater.Inflate(Resource.Menu.main_botton_drawer_menu, mWearableActionDrawer.Menu);
                }
            }
        }
    }

    internal class NavDrawerAdapter : WearableNavigationDrawerView.WearableNavigationDrawerAdapter
    {
        private Context mContext;
        private readonly List<NavDrawerItem> navDrawerItems = new List<NavDrawerItem>()
        {
            new NavDrawerItem(Resource.String.label_condition, Resource.Drawable.ic_logo),
            new NavDrawerItem(Resource.String.title_fragment_alerts, Resource.Drawable.ic_error_white),
            new NavDrawerItem(Resource.String.label_forecast, Resource.Drawable.ic_date_range_black_24dp),
            new NavDrawerItem(Resource.String.label_hourlyforecast, Resource.Drawable.ic_access_time_black_24dp),
            new NavDrawerItem(Resource.String.label_details, Resource.Drawable.ic_list_black_24dp),
        };
        private List<NavDrawerItem> navItems;
        public WeatherNowViewModel WeatherNowView { get; private set; }

        public NavDrawerAdapter(Context context)
        {
            mContext = context;
            navItems = navDrawerItems;
        }

        public override int Count => navItems.Count;

        public override Drawable GetItemDrawable(int position)
        {
            var drawable = mContext.GetDrawable(navItems[position].DrawableIcon);
            drawable.SetTint(mContext.GetColor(Android.Resource.Color.White));
            return drawable;
        }

        public override ICharSequence GetItemTextFormatted(int position)
        {
            return new Java.Lang.String(mContext.GetString(navItems[position].TitleString));
        }

        public int GetStringId(int position)
        {
            return navItems[position].TitleString;
        }

        public void UpdateNavDrawerItems(WeatherNowViewModel weatherNowView)
        {
            WeatherNowView = weatherNowView;

            IEnumerable<NavDrawerItem> items = navDrawerItems;

            if (weatherNowView.Extras.Alerts.Count == 0)
                items = items.Where(item => item.TitleString != Resource.String.title_fragment_alerts);
            if (weatherNowView.Extras.HourlyForecast.Count == 0)
                items = items.Where(item => item.TitleString != Resource.String.label_hourlyforecast);

            navItems = items.ToList();
            NotifyDataSetChanged();
        }
    }

    internal class NavDrawerItem : Java.Lang.Object
    {
        public int TitleString { get; set; }
        public int DrawableIcon { get; set; }

        public NavDrawerItem(int TitleString, int DrawableIcon)
        {
            this.TitleString = TitleString;
            this.DrawableIcon = DrawableIcon;
        }
    }
}