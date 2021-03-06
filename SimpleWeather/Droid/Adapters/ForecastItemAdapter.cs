﻿#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Linq;
using Android.Views;
using Android.Support.V7.Widget;
using Android.Util;
#if __ANDROID_WEAR__
using SimpleWeather.Droid.Wear;
using SimpleWeather.Droid.Wear.Controls;
#else
using SimpleWeather.Droid.App;
using SimpleWeather.Droid.App.Controls;
#endif
using SimpleWeather.Controls;

namespace SimpleWeather.Droid.Adapters
{
    public class ForecastItemAdapter : RecyclerView.Adapter
    {
        private List<ForecastItemViewModel> mDataset;

        // Provide a reference to the views for each data item
        // Complex data items may need more than one view per item, and
        // you provide access to all the views for a data item in a view holder
        class ViewHolder : RecyclerView.ViewHolder
        {
            public ForecastItem mForecastItem;
            public ViewHolder(ForecastItem v)
                : base(v)
            {
                mForecastItem = v;
            }
        }

        // Provide a suitable constructor (depends on the kind of dataset)
        public ForecastItemAdapter(List<ForecastItemViewModel> myDataset)
        {
            mDataset = myDataset;
        }

        // Create new views (invoked by the layout manager)
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            // create a new view
            ForecastItem v = new ForecastItem(parent.Context);
#if __ANDROID_WEAR__
            // set the view's size, margins, paddings and layout parameters
            v.LayoutParameters = new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            int paddingHoriz = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 16, parent.Context.Resources.DisplayMetrics);
            v.SetPaddingRelative(paddingHoriz, 0, paddingHoriz, 0);
#endif
            return new ViewHolder(v);
        }

        // Replace the contents of a view (invoked by the layout manager)
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            // - get element from your dataset at this position
            // - replace the contents of the view with that element
            ViewHolder vh = holder as ViewHolder;
            vh.mForecastItem.SetForecast(mDataset[position]);
        }

        // Return the size of your dataset (invoked by the layout manager)
        public override int ItemCount => mDataset.Count;

        public void UpdateItems(IEnumerable<ForecastItemViewModel> items)
        {
            mDataset.Clear();
            mDataset.AddRange(items);
            NotifyDataSetChanged();
        }
    }
}
#endif