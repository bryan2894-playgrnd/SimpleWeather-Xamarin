﻿using System;
using System.Linq;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Support.V4.App;
using Android.Webkit;
using Android.Util;
using Android.Support.V4.Content;
using Android;
using Android.Content.PM;
using Android.Runtime;
using Android.Widget;
using SimpleWeather.Utils;
using Android.Text.Style;
using Android.Graphics;
using Android.Text;
using Android.Support.V7.Preferences;
using SimpleWeather.Droid.App.Widgets;
using System.Collections.Generic;
using SimpleWeather.Droid.Helpers;

namespace SimpleWeather.Droid.App
{
    [Android.App.Activity(Label = "@string/title_activity_settings", Theme = "@style/SettingsTheme")]
    public class SettingsActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);

            // Display the fragment as the main content.
            Fragment fragment = SupportFragmentManager.FindFragmentById(Android.Resource.Id.Content);

            // Check if fragment exists
            if (fragment == null)
            {
                SupportFragmentManager.BeginTransaction()
                        .Replace(Android.Resource.Id.Content, new SettingsFragment())
                        .Commit();
            }
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Android.Resource.Id.Home)
            {
                if (!base.OnOptionsItemSelected(item))
                {
                    OnBackPressed();
                }
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        public override void OnBackPressed()
        {
            string KEY_API = "API";

            if (SupportFragmentManager.FindFragmentById(Android.Resource.Id.Content) is SettingsFragment fragment)
            {
                var keyPref = fragment.FindPreference(KEY_API) as ListPreference;
                if (Settings.UsePersonalKey && String.IsNullOrWhiteSpace(Settings.API_KEY) && WeatherData.WeatherManager.IsKeyRequired(keyPref.Value))
                {
                    // Set keyentrypref color to red
                    Toast.MakeText(this, Resource.String.message_enter_apikey, ToastLength.Long).Show();
                    return;
                }
            }

            base.OnBackPressed();
        }

        public class SettingsFragment : PreferenceFragmentCompat, ActivityCompat.IOnRequestPermissionsResultCallback,
            ISharedPreferencesOnSharedPreferenceChangeListener
        {
            private const int PERMISSION_LOCATION_REQUEST_CODE = 0;

            // Preference Keys
            private const string KEY_ABOUTAPP = "key_aboutapp";
            private const string KEY_FOLLOWGPS = "key_followgps";
            private const string KEY_API = "API";
            private const string KEY_APIKEY = "API_KEY";
            private const string KEY_USECELSIUS = "key_usecelsius";
            private const string KEY_REFRESHINTERVAL = "key_refreshinterval";
            private const string KEY_ONGOINGNOTIFICATION = "key_ongoingnotification";
            private const string KEY_NOTIFICATIONICON = "key_notificationicon";
            private const string KEY_USEALERTS = "key_usealerts";
            private const string KEY_APIREGISTER = "key_apiregister";
            private const string KEY_USEPERSONALKEY = "key_usepersonalkey";

            private const string CATEGORY_NOTIFICATION = "category_notification";
            private const string CATEGORY_API = "category_api";

            // Preferences
            private SwitchPreferenceCompat followGps;
            private DropDownPreference providerPref;
            private SwitchPreferenceCompat personalKeyPref;
            private EditTextPreference keyEntry;
            private SwitchPreferenceCompat onGoingNotification;
            private DropDownPreference notificationIcon;
            private SwitchPreferenceCompat alertNotification;
            private Preference registerPref;

            private PreferenceCategory notCategory;
            private PreferenceCategory apiCategory;

            // Intent queue
            private HashSet<Intent> intentQueue;

            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_general, null);
                HasOptionsMenu = false;

                notCategory = (PreferenceCategory)FindPreference(CATEGORY_NOTIFICATION);
                apiCategory = (PreferenceCategory)FindPreference(CATEGORY_API);

                FindPreference(KEY_ABOUTAPP).PreferenceClick += (object sender, Preference.PreferenceClickEventArgs e) =>
                {
                    // Display the fragment as the main content.
                    FragmentManager.BeginTransaction()
                            .Replace(Android.Resource.Id.Content, new AboutAppFragment())
                            .AddToBackStack(null)
                            .Commit();
                };

                followGps = (SwitchPreferenceCompat)FindPreference(KEY_FOLLOWGPS);
                followGps.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var pref = e.Preference as SwitchPreferenceCompat;
                    if ((bool)e.NewValue)
                    {
                        if (ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.AccessFineLocation) != Permission.Granted &&
                            ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
                        {
                            ActivityCompat.RequestPermissions(this.Activity, new String[] { Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation },
                                    PERMISSION_LOCATION_REQUEST_CODE);
                            return;
                        }
                        else
                        {
                            // Reset home location data
                            //Settings.SaveLastGPSLocData(new WeatherData.LocationData());
                        }
                    }
                };

                keyEntry = (EditTextPreference)FindPreference(KEY_APIKEY);
                personalKeyPref = (SwitchPreferenceCompat)FindPreference(KEY_USEPERSONALKEY);
                personalKeyPref.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var pref = e.Preference as SwitchPreferenceCompat;
                    if ((bool)e.NewValue)
                    {
                        if (apiCategory.FindPreference(KEY_APIKEY) == null)
                            apiCategory.AddPreference(keyEntry);
                        if (apiCategory.FindPreference(KEY_APIREGISTER) == null)
                            apiCategory.AddPreference(registerPref);
                        keyEntry.Enabled = true;
                    }
                    else
                    {
                        var selectedWProv = WeatherData.WeatherManager.GetProvider(providerPref.Value);

                        if (!String.IsNullOrWhiteSpace(selectedWProv.GetAPIKey()))
                        {
                            // We're using our own (verified) keys
                            Settings.KeyVerified = true;
                            Settings.API = providerPref.Value;
                        }

                        keyEntry.Enabled = false;
                        apiCategory.RemovePreference(keyEntry);
                        apiCategory.RemovePreference(registerPref);
                    }
                };

                var providers = WeatherData.WeatherAPI.APIs;
                providerPref = (DropDownPreference)FindPreference(KEY_API);
                providerPref.SetEntries(providers.Select(provider => provider.Display).ToArray());
                providerPref.SetEntryValues(providers.Select(provider => provider.Value).ToArray());
                providerPref.Persistent = false;
                providerPref.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var pref = e.Preference as ListPreference;
                    var selectedWProv = WeatherData.WeatherManager.GetProvider(e.NewValue.ToString());

                    if (selectedWProv.KeyRequired)
                    {
                        if (String.IsNullOrWhiteSpace(selectedWProv.GetAPIKey()))
                        {
                            personalKeyPref.Checked = Settings.UsePersonalKey = true;
                            personalKeyPref.Enabled = false;
                            keyEntry.Enabled = false;
                            apiCategory.RemovePreference(keyEntry);
                            apiCategory.RemovePreference(registerPref);
                        }
                        else
                        {
                            personalKeyPref.Enabled = true;
                        }

                        if (!Settings.UsePersonalKey)
                        {
                            // We're using our own (verified) keys
                            Settings.KeyVerified = true;
                            keyEntry.Enabled = false;
                            apiCategory.RemovePreference(keyEntry);
                            apiCategory.RemovePreference(registerPref);
                        }
                        else
                        {
                            keyEntry.Enabled = true;

                            if (apiCategory.FindPreference(KEY_APIKEY) == null)
                                apiCategory.AddPreference(keyEntry);
                            if (apiCategory.FindPreference(KEY_APIREGISTER) == null)
                                apiCategory.AddPreference(registerPref);
                        }

                        if (apiCategory.FindPreference(KEY_USEPERSONALKEY) == null)
                            apiCategory.AddPreference(personalKeyPref);

                        // Reset to old value if not verified
                        if (!Settings.KeyVerified)
                            Settings.API = pref.Value;
                        else
                            Settings.API = e.NewValue.ToString();

                        var providerEntry = providers.Find(provider => provider.Value == e.NewValue.ToString());
                        UpdateKeySummary(providerEntry.Display);
                        UpdateRegisterLink(providerEntry.Value);
                    }
                    else
                    {
                        Settings.KeyVerified = false;
                        keyEntry.Enabled = false;
                        personalKeyPref.Enabled = false;

                        Settings.API = e.NewValue.ToString();
                        // Clear API KEY entry to avoid issues
                        Settings.API_KEY = String.Empty;

                        apiCategory.RemovePreference(personalKeyPref);
                        apiCategory.RemovePreference(keyEntry);
                        apiCategory.RemovePreference(registerPref);
                        UpdateKeySummary();
                        UpdateRegisterLink();
                    }

                    UpdateAlertPreference(WeatherData.WeatherManager.GetInstance().SupportsAlerts);
                };

                registerPref = FindPreference(KEY_APIREGISTER);

                // Set key as verified if API Key is req for API and its set
                if (WeatherData.WeatherManager.GetInstance().KeyRequired)
                {
                    keyEntry.Enabled = true;

                    if (!String.IsNullOrWhiteSpace(Settings.API_KEY) && !Settings.KeyVerified)
                        Settings.KeyVerified = true;

                    if (String.IsNullOrWhiteSpace(WeatherData.WeatherManager.GetInstance().GetAPIKey()))
                    {
                        personalKeyPref.Checked = Settings.UsePersonalKey = true;
                        personalKeyPref.Enabled = false;
                        keyEntry.Enabled = false;
                        apiCategory.RemovePreference(keyEntry);
                        apiCategory.RemovePreference(registerPref);
                    }
                    else
                    {
                        personalKeyPref.Enabled = true;
                    }

                    if (!Settings.UsePersonalKey)
                    {
                        // We're using our own (verified) keys
                        Settings.KeyVerified = true;
                        keyEntry.Enabled = false;
                        apiCategory.RemovePreference(keyEntry);
                        apiCategory.RemovePreference(registerPref);
                    }
                    else
                    {
                        keyEntry.Enabled = true;

                        if (apiCategory.FindPreference(KEY_APIKEY) == null)
                            apiCategory.AddPreference(keyEntry);
                        if (apiCategory.FindPreference(KEY_APIREGISTER) == null)
                            apiCategory.AddPreference(registerPref);
                    }
                }
                else
                {
                    keyEntry.Enabled = false;
                    personalKeyPref.Enabled = false;
                    apiCategory.RemovePreference(personalKeyPref);
                    apiCategory.RemovePreference(keyEntry);
                    apiCategory.RemovePreference(registerPref);
                    Settings.KeyVerified = false;
                }

                UpdateKeySummary();
                UpdateRegisterLink();

                onGoingNotification = (SwitchPreferenceCompat)FindPreference(KEY_ONGOINGNOTIFICATION);
                onGoingNotification.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var pref = e.Preference as SwitchPreferenceCompat;
                    var context = App.Context;

                    // On-going notification
                    if ((bool)e.NewValue)
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_REFRESHNOTIFICATION));

                        if (notCategory.FindPreference(KEY_NOTIFICATIONICON) == null)
                            notCategory.AddPreference(notificationIcon);
                    }
                    else
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_REMOVENOTIFICATION));

                        notCategory.RemovePreference(notificationIcon);
                    }
                };

                notificationIcon = (DropDownPreference)FindPreference(KEY_NOTIFICATIONICON);
                notificationIcon.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var context = App.Context;
                    WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                        .SetAction(WeatherWidgetService.ACTION_REFRESHNOTIFICATION));
                };

                // Remove preferences
                if (!onGoingNotification.Checked)
                {
                    notCategory.RemovePreference(notificationIcon);
                }

                alertNotification = (SwitchPreferenceCompat)FindPreference(KEY_USEALERTS);
                alertNotification.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var pref = e.Preference as SwitchPreferenceCompat;
                    var context = App.Context;

                    // Alert notification
                    if ((bool)e.NewValue)
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_STARTALARM));
                    }
                    else
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_CANCELALARM));
                    }
                };
                UpdateAlertPreference(WeatherData.WeatherManager.GetInstance().SupportsAlerts);
            }

            public override void OnDisplayPreferenceDialog(Preference preference)
            {
                const String TAG = nameof(KeyEntryPreferenceDialogFragment);

                if (FragmentManager.FindFragmentByTag(TAG) != null)
                    return;

                if (preference is EditTextPreference && preference.Key == KEY_APIKEY)
                {
                    var fragment = KeyEntryPreferenceDialogFragment.NewInstance(providerPref.Value, preference.Key);
                    fragment.PositiveButtonClick += async (sender, e) =>
                    {
                        String key = fragment.Key;

                        String API = providerPref.Value;
                        if (await WeatherData.WeatherManager.IsKeyValid(key, API))
                        {
                            Settings.API_KEY = key;
                            Settings.API = API;

                            Settings.KeyVerified = true;
                            UpdateKeySummary();
                            UpdateAlertPreference(WeatherData.WeatherManager.GetInstance().SupportsAlerts);

                            fragment.Dialog.Dismiss();
                        }
                    };

                    fragment.SetTargetFragment(this, 0);
                    fragment.Show(this.FragmentManager, TAG);
                }
                else
                {
                    base.OnDisplayPreferenceDialog(preference);
                }
            }

            private void UpdateKeySummary()
            {
                UpdateKeySummary(providerPref.Entry);
            }

            private void UpdateKeySummary(string providerAPI)
            {
                if (!String.IsNullOrWhiteSpace(Settings.API_KEY))
                {
                    var keyVerified = Settings.KeyVerified;

                    var colorSpan = new ForegroundColorSpan(keyVerified ?
                        Color.Green : Color.Red);
                    ISpannable summary = new SpannableString(keyVerified ?
                        GetString(Resource.String.message_keyverified) : GetString(Resource.String.message_keyinvalid));
                    summary.SetSpan(colorSpan, 0, summary.Length(), 0);
                    keyEntry.SummaryFormatted = summary;
                }
                else
                {
                    keyEntry.Summary = Activity.GetString(Resource.String.pref_summary_apikey, providerAPI);
                }
            }

            private void UpdateRegisterLink()
            {
                UpdateRegisterLink(providerPref.Value);
            }

            private void UpdateRegisterLink(string providerAPI)
            {
                registerPref.Intent = new Intent(Intent.ActionView)
                    .SetData(Android.Net.Uri.Parse(
                        WeatherData.WeatherAPI.APIs.First(prov => prov.Value == providerAPI).APIRegisterURL));
            }

            private void UpdateAlertPreference(bool enable)
            {
                alertNotification.Enabled = enable;
                alertNotification.Summary = enable ?
                    Activity.GetString(Resource.String.pref_summary_alerts) : Activity.GetString(Resource.String.pref_summary_alerts_disabled);
            }

            public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
            {
                switch (requestCode)
                {
                    case PERMISSION_LOCATION_REQUEST_CODE:
                        {
                            // If request is cancelled, the result arrays are empty.
                            if (grantResults.Length > 0
                                    && grantResults[0] == Permission.Granted)
                            {
                                // permission was granted, yay!
                                // Do the task you need to do.
                                // Reset home location data
                                //Settings.SaveLastGPSLocData(new WeatherData.LocationData());
                            }
                            else
                            {
                                // permission denied, boo! Disable the
                                // functionality that depends on this permission.
                                Toast.MakeText(Activity, Resource.String.error_location_denied, ToastLength.Short).Show();
                                followGps.Checked = false;
                            }
                            return;
                        }
                    default:
                        break;
                }
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                var activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.title_activity_settings);

                // Register listener
                App.Preferences.UnregisterOnSharedPreferenceChangeListener(App.SharedPreferenceListener);
                App.Preferences.RegisterOnSharedPreferenceChangeListener(this);

                // Initialize queue
                intentQueue = new HashSet<Intent>(new IntentEqualityComparer());
            }

            public override void OnPause()
            {
                // Unregister listener
                App.Preferences.UnregisterOnSharedPreferenceChangeListener(this);
                App.Preferences.RegisterOnSharedPreferenceChangeListener(App.SharedPreferenceListener);

                // Process queue
                foreach (Intent intent in intentQueue)
                {
                    if (intent.Component.ClassName.Equals(Java.Lang.Class.FromType(typeof(WeatherWidgetService)).Name))
                    {
                        WeatherWidgetService.EnqueueWork(Activity, intent);
                    }
                    else
                    {
                        this.Activity.StartService(intent);
                    }
                }

                base.OnPause();
            }

            public bool EnqueueIntent(Intent intent)
            {
                if (intent == null)
                    return false;
                else
                {
                    return intentQueue.Add(intent);
                }
            }

            public void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
            {
                if (String.IsNullOrWhiteSpace(key))
                    return;

                Context context = Activity;

                switch (key)
                {
                    // Weather Provider changed
                    case KEY_API:
                        WeatherData.WeatherManager.GetInstance().UpdateAPI();
                        EnqueueIntent(new Intent(context, typeof(WearableDataListenerService))
                                .SetAction(WearableDataListenerService.ACTION_SENDSETTINGSUPDATE));
                        goto case KEY_USECELSIUS;
                    // FollowGPS changed
                    case KEY_FOLLOWGPS:
                        EnqueueIntent(new Intent(context, typeof(WearableDataListenerService))
                                .SetAction(WearableDataListenerService.ACTION_SENDSETTINGSUPDATE));
                        EnqueueIntent(new Intent(context, typeof(WearableDataListenerService))
                                .SetAction(WearableDataListenerService.ACTION_SENDLOCATIONUPDATE));
                        goto case KEY_USECELSIUS;
                    // Settings unit changed
                    case KEY_USECELSIUS:
                        EnqueueIntent(new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_UPDATEWEATHER));
                        break;
                    // Refresh interval changed
                    case KEY_REFRESHINTERVAL:
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_UPDATEALARM));
                        break;
                    default:
                        break;
                }
            }
        }

        public class KeyEntryPreferenceDialogFragment : EditTextPreferenceDialogFragmentCompat
        {
            public EventHandler PositiveButtonClick { get; set; }
            public EventHandler NegativeButtonClick { get; set; }

            private String CurrentAPI { get; }
            public String Key { get; set; }

            private EditText keyEntry;
            private EditText keyEntry2;

            public KeyEntryPreferenceDialogFragment() :
                base()
            {
                if (String.IsNullOrWhiteSpace(CurrentAPI))
                    CurrentAPI = Settings.API;
            }

            public KeyEntryPreferenceDialogFragment(String CurrentAPI) :
                base()
            {
                this.CurrentAPI = CurrentAPI;
            }

            public static KeyEntryPreferenceDialogFragment NewInstance(String API, String key)
            {
                var fragment = new KeyEntryPreferenceDialogFragment(API);
                Bundle b = new Bundle(1);
                b.PutString(ArgKey, key);
                fragment.Arguments = b;
                return fragment;
            }

            protected override View OnCreateDialogView(Context context)
            {
                if (CurrentAPI == WeatherData.WeatherAPI.Here)
                {
                    LayoutInflater inflater = LayoutInflater.From(context);
                    return inflater.Inflate(Resource.Layout.layout_keyentry2_dialog, null);
                }
                else
                {
                    return base.OnCreateDialogView(context);
                }
            }

            protected override void OnBindDialogView(View view)
            {
                base.OnBindDialogView(view);

                keyEntry = view.FindViewById<EditText>(Android.Resource.Id.Edit);
                keyEntry.TextChanged += EditText_TextChanged;

                if (CurrentAPI == WeatherData.WeatherAPI.Here)
                {
                    keyEntry2 = view.FindViewById<EditText>(Resource.Id.keyEntry2);
                    keyEntry2.TextChanged += EditText_TextChanged;
                }
            }

            private void EditText_TextChanged(object sender, TextChangedEventArgs e)
            {
                if (CurrentAPI == WeatherData.WeatherAPI.Here)
                {
                    string app_id = keyEntry?.Text;
                    string app_code = keyEntry2?.Text;

                    Key = String.Format("{0};{1}", app_id, app_code);
                }
                else
                    Key = e.Text.ToString();
            }

            public override void SetupDialog(Android.App.Dialog dialog, int style)
            {
                base.SetupDialog(dialog, style);
                var alertdialog = dialog as AlertDialog;
                alertdialog.ShowEvent += (s, e) =>
                {
                    View posButton = alertdialog.GetButton((int)DialogButtonType.Positive);
                    View negButton = alertdialog.GetButton((int)DialogButtonType.Negative);
                    posButton.Click += PositiveButtonClick;
                    if (NegativeButtonClick == null)
                        negButton.Click += delegate { dialog.Dismiss(); };
                    else
                        negButton.Click += NegativeButtonClick;
                };

                Key = Settings.API_KEY;

                if (CurrentAPI == WeatherData.WeatherAPI.Here)
                {
                    string app_id = String.Empty;
                    string app_code = String.Empty;

                    if (!String.IsNullOrWhiteSpace(Key))
                    {
                        var keyArr = Key.Split(';');
                        app_id = keyArr.First();
                        app_code = keyArr.Last();
                    }

                    keyEntry.Text = app_id;
                    keyEntry2.Text = app_code;
                }
            }
        }

        public class AboutAppFragment : PreferenceFragmentCompat
        {
            // Preference Keys
            private static string KEY_ABOUTCREDITS = "key_aboutcredits";
            private static string KEY_ABOUTOSLIBS = "key_aboutoslibs";
            private static string KEY_ABOUTVERSION = "key_aboutversion";

            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_aboutapp, null);
                HasOptionsMenu = false;

                FindPreference(KEY_ABOUTCREDITS).PreferenceClick += (object sender, Preference.PreferenceClickEventArgs e) =>
                {
                    // Display the fragment as the main content.
                    FragmentManager.BeginTransaction()
                            .Replace(Android.Resource.Id.Content, new CreditsFragment())
                            .AddToBackStack(null)
                            .Commit();
                };
                FindPreference(KEY_ABOUTOSLIBS).PreferenceClick += (object sender, Preference.PreferenceClickEventArgs e) =>
                {
                    // Display the fragment as the main content.
                    FragmentManager.BeginTransaction()
                            .Replace(Android.Resource.Id.Content, new OSSCreditsFragment())
                            .AddToBackStack(null)
                            .Commit();
                };

                var packageInfo = Activity.PackageManager.GetPackageInfo(Activity.PackageName, 0);
                FindPreference(KEY_ABOUTVERSION).Summary = string.Format("v{0}", packageInfo.VersionName);
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                var activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.pref_title_about);
            }
        }

        public class CreditsFragment : PreferenceFragmentCompat
        {
            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_credits, null);
                HasOptionsMenu = false;
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                var activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.pref_title_credits);
            }
        }

        public class OSSCreditsFragment : PreferenceFragmentCompat
        {
            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_oslibs, null);
                HasOptionsMenu = false;
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                var activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.pref_title_oslibs);
            }
        }

        public class OSSCreditsPreference : Preference
        {
            public OSSCreditsPreference(Context context)
                : base(context)
            {
            }

            public OSSCreditsPreference(Context context, IAttributeSet attrs)
                : base(context, attrs)
            {
            }

            public OSSCreditsPreference(Context context, IAttributeSet attrs, int defStyleAttr)
                : base(context, attrs, defStyleAttr)
            {
            }

            public OSSCreditsPreference(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
                : base(context, attrs, defStyleAttr, defStyleRes)
            {
            }

            public override void OnBindViewHolder(PreferenceViewHolder holder)
            {
                base.OnBindViewHolder(holder);

                WebView webview = holder.ItemView.FindViewById<WebView>(Resource.Id.webview);
                webview.Settings.SetLayoutAlgorithm(WebSettings.LayoutAlgorithm.SingleColumn);
                webview.LoadUrl("file:///android_asset/credits/licenses.html");
            }
        }

        public class CustomDropDownPreference : DropDownPreference
        {
            public CustomDropDownPreference(Context context)
                : base(context)
            {
            }

            public CustomDropDownPreference(Context context, IAttributeSet attrs)
                : base(context, attrs)
            {
            }

            public CustomDropDownPreference(Context context, IAttributeSet attrs, int defStyleAttr)
                : base(context, attrs, defStyleAttr)
            {
            }

            public CustomDropDownPreference(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
                : base(context, attrs, defStyleAttr, defStyleRes)
            {
            }

            public CustomDropDownPreference(IntPtr handle, JniHandleOwnership transer)
                : base(handle, transer)
            {
            }

            protected override ArrayAdapter CreateAdapter()
            {
                return new ArrayAdapter(Context, Resource.Layout.dropdown_item);
            }

            public override void OnBindViewHolder(PreferenceViewHolder holder)
            {
                base.OnBindViewHolder(holder);

                float scale = Context.Resources.DisplayMetrics.Density;

                // Show spinner under preference title
                Spinner spinner = holder.ItemView.FindViewById<Spinner>(Resource.Id.spinner);
                int titlePx = (int)TypedValue.ApplyDimension(ComplexUnitType.Sp, 14, Context.Resources.DisplayMetrics);
                int paddingPx = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 16, Context.Resources.DisplayMetrics);
                spinner.DropDownVerticalOffset = titlePx + paddingPx - 1;
            }
        }
    }
}