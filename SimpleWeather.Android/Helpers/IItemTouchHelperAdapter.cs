﻿namespace SimpleWeather.Droid.App.Helpers
{
    public interface IItemTouchHelperAdapter
    {
        void OnItemMove(int fromPosition, int toPosition);

        void OnItemMoved(int fromPosition, int toPosition);

        void OnItemDismiss(int position);
    }
}