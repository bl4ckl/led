﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;

namespace Led.UserControls.Timeline
{
    class TimelineTimeTooltipUserControl : Grid
    {
        public TimeSpan Time
        {
            set
            {
                string res = value.Minutes.ToString().PadLeft(2, '0') + ":" + value.Seconds.ToString().PadLeft(2, '0') + ":" + value.Milliseconds.ToString().PadLeft(3, '0');
                _Label.Content = res;
            }
        }

        public double XOffset
        {
            get => (RenderTransform as TranslateTransform).X;
            set => (RenderTransform as TranslateTransform).X = value;
        }

        private Label _Label;

        public TimelineTimeTooltipUserControl(TimeSpan time)
        {
            RenderTransform = new TranslateTransform();
            Background = Brushes.White;

            _Label = new Label();
            _Label.Content = "00:00:00";
            _Label.Margin = new Thickness(0);
            _Label.Padding = new Thickness(1);
            _Label.BorderBrush = Brushes.Aqua;
            _Label.BorderThickness = new Thickness(1);

            Time = time;

            Children.Add(_Label);
        }

        protected override void OnRender(DrawingContext dc)
        {
            MaxHeight = ((Canvas)Parent).ActualHeight;
            _Label.FontSize = MaxHeight > 0 ? 0.6 * MaxHeight : 10;
            base.OnRender(dc);
        }
    }
}