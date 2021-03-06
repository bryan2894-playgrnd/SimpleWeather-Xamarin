﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Wearable.Views;
using Android.Views;
using Android.Widget;

namespace SimpleWeather.Droid.Wear.Helpers
{
    public class AcceptDenyDialogBuilder : IDisposable
    {
        private Context mContext;
        private String Message;
        private EventHandler<DialogClickEventArgs> NegativeButtonEvent;
        private EventHandler<DialogClickEventArgs> PositiveButtonEvent;
        private IDialogInterfaceOnClickListener clickListener;

        public void Dispose()
        {
            clickListener.Dispose();
            GC.SuppressFinalize(this);
        }

        public AcceptDenyDialogBuilder(Context context)
        {
            mContext = context;
            clickListener = new AcceptDenyDialogListener(this);
        }

        private void Apply(AcceptDenyDialog dialog)
        {
            dialog.SetMessage(Message);
            dialog.SetPositiveButton(clickListener);
            dialog.SetNegativeButton(clickListener);
        }

        public AcceptDenyDialogBuilder SetMessage(int resId)
        {
            Message = mContext.GetString(resId);
            return this;
        }

        public AcceptDenyDialogBuilder SetPositiveButton(EventHandler<DialogClickEventArgs> handler)
        {
            PositiveButtonEvent = handler;
            return this;
        }

        public AcceptDenyDialogBuilder SetNegativeButton(EventHandler<DialogClickEventArgs> handler)
        {
            NegativeButtonEvent = handler;
            return this;
        }

        public AcceptDenyDialog Create()
        {
            var dialog = new AcceptDenyDialog(mContext);
            dialog.Create();
            Apply(dialog);
            return dialog;
        }

        public AcceptDenyDialog Show()
        {
            var dialog = this.Create();
            dialog.Show();
            return dialog;
        }

        private class AcceptDenyDialogListener : Java.Lang.Object, IDialogInterfaceOnClickListener
        {
            private AcceptDenyDialogBuilder builder;

            public AcceptDenyDialogListener(AcceptDenyDialogBuilder builder)
            {
                this.builder = builder;
            }

            public void OnClick(IDialogInterface dialog, int which)
            {
                switch (which)
                {
                    case (int)DialogButtonType.Positive:
                        builder.PositiveButtonEvent?.Invoke(dialog, new DialogClickEventArgs(which));
                        break;
                    case (int)DialogButtonType.Negative:
                        builder.NegativeButtonEvent?.Invoke(dialog, new DialogClickEventArgs(which));
                        break;
                    case (int)DialogButtonType.Neutral:
                        break;
                }
            }
        }
    }
}