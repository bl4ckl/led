﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Diagnostics;
using Led.ViewModels;

namespace Led.Utility.Timeline
{
    public enum TimeLineManipulationMode { Linked, Free }
    internal enum TimeLineAction { Move, StretchStart, StretchEnd }

    internal class TimeLineItemChangedEventArgs : EventArgs
    {
        public TimeLineManipulationMode Mode { get; set; }
        public TimeLineAction Action { get; set; }
        public TimeSpan DeltaTime { get; set; }
        public double DeltaX { get; set; }
    }

    internal class TimeLineDragAdorner : Adorner
    {
        private ContentPresenter _adorningContentPresenter;
        internal EffectBaseVM Data { get; set; }
        internal DataTemplate Template { get; set; }
        Point _mousePosition;
        public Point MousePosition
        {
            get
            {
                return _mousePosition;
            }
            set
            {
                if (_mousePosition != value)
                {
                    _mousePosition = value;
                    _layer.Update(AdornedElement);
                }

            }
        }

        AdornerLayer _layer;
        public TimeLineDragAdorner(TimeLineItemControl uiElement, DataTemplate template)
            : base(uiElement)
        {
            _adorningContentPresenter = new ContentPresenter();
            _adorningContentPresenter.Content = uiElement.DataContext;
            _adorningContentPresenter.ContentTemplate = template;
            _adorningContentPresenter.Opacity = 0.5;
            _layer = AdornerLayer.GetAdornerLayer(uiElement);

            _layer.Add(this);
            IsHitTestVisible = false;

        }
        public void Detach()
        {
            _layer.Remove(this);
        }
        protected override Visual GetVisualChild(int index)
        {
            return _adorningContentPresenter;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            //_adorningContentPresenter.Measure(constraint);
            return new Size((AdornedElement as TimeLineItemControl).Width, (AdornedElement as TimeLineItemControl).DesiredSize.Height);//(_adorningContentPresenter.Width,_adorningContentPresenter.Height);
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _adorningContentPresenter.Arrange(new Rect(finalSize));
            return finalSize;
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            GeneralTransformGroup result = new GeneralTransformGroup();
            result.Children.Add(base.GetDesiredTransform(transform));
            result.Children.Add(new TranslateTransform(MousePosition.X - 4, MousePosition.Y - 4));
            return result;
        }

    }

    public enum TimeLineViewLevel { Minutes, Hours, Days, Weeks, Months, Years };
    //public class TimeLineControl : ListBox


    public class TimeLineControl : Canvas
    {
        public static TimeSpan CalculateMinimumAllowedTimeSpan(double unitSize)
        {
            //minute = unitsize*pixels
            //desired minimum widh for these manipulations = 10 pixels
            int minPixels = 10;
            double hours = minPixels / unitSize;
            //convert to milliseconds
            long ticks = (long)(hours * 60 * 60000 * 10000);
            return new TimeSpan(ticks);
        }

        private double _BumpThreshold = 1.5;
        private ScrollViewer _ScrollViewer;
        private Canvas _GridCanvas;
        static TimeLineDragAdorner _dragAdorner;
        static TimeLineDragAdorner DragAdorner
        {
            get
            {
                return _dragAdorner;
            }
            set
            {
                if (_dragAdorner != null)
                    _dragAdorner.Detach();
                _dragAdorner = value;
            }
        }
        public bool SynchedWithSiblings { get; set; } = true;
        internal bool _isSynchInstigator = false;
        internal double SynchWidth = 0;

        bool _itemsInitialized = false;

        bool _unitSizeInitialized = false;
        bool _startDateInitialized = false;


        #region dependency properties


        public EffectBaseVM FocusOnItem
        {
            get { return (EffectBaseVM)GetValue(FocusOnItemProperty); }
            set { SetValue(FocusOnItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FocusOnItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FocusOnItemProperty =
            DependencyProperty.Register("FocusOnItem", typeof(EffectBaseVM), typeof(TimeLineControl), new UIPropertyMetadata(null, new PropertyChangedCallback(FocusItemChanged)));
        public static void FocusItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if ((e.NewValue != null) && (tc != null))
            {
                tc.ScrollToItem(e.NewValue as EffectBaseVM);
            }
        }

        private void ScrollToItem(EffectBaseVM target)
        {
            double tgtNewWidth = 0;
            double maxUnitSize = 450;//28000;
            double minUnitSize = 1;
            if (_ScrollViewer != null)
            {
                for (int i = 1; i < Children.Count; i++)
                {
                    var ctrl = Children[i] as TimeLineItemControl;
                    if (ctrl != null && ctrl.DataContext == target)
                    {
                        double curW = ctrl.Width;
                        if (curW < 5)
                        {
                            tgtNewWidth = 50;
                        }
                        else if (curW > _ScrollViewer.ViewportWidth)
                        {
                            tgtNewWidth = _ScrollViewer.ViewportWidth / 3;
                        }

                        if (tgtNewWidth != 0)
                        {
                            double newUnitSize = (UnitSize * tgtNewWidth) / curW;
                            if (newUnitSize > maxUnitSize)
                                newUnitSize = maxUnitSize;
                            else if (newUnitSize < minUnitSize)
                                newUnitSize = minUnitSize;
                            UnitSize = newUnitSize;
                            SynchronizeSiblings();
                        }
                        ctrl.BringIntoView();
                        return;
                    }
                }
            }
        }



        #region minwidth
        public double MinWidth
        {
            get { return (double)GetValue(MinWidthProperty); }
            set { SetValue(MinWidthProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinWidth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinWidthProperty =
            DependencyProperty.Register("MinWidth", typeof(double), typeof(TimeLineControl), new UIPropertyMetadata(0.0));
        #endregion

        #region maxwidth
        public ushort TotalFrames
        {
            get { return (ushort)GetValue(TotalFramesProperty); }
            set { SetValue(TotalFramesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinWidth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TotalFramesProperty =
            DependencyProperty.Register("TotalFrames", typeof(ushort), typeof(TimeLineControl), new UIPropertyMetadata((ushort)0));
        #endregion

        #region minheight
        public double MinHeight
        {
            get { return (double)GetValue(MinHeightProperty); }
            set { SetValue(MinHeightProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinHeight.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinHeightProperty =
            DependencyProperty.Register("MinHeight", typeof(double), typeof(TimeLineControl), new UIPropertyMetadata(0.0));
        #endregion

        #region background and grid dependency properties
        #region minimum unit width
        public double MinimumUnitWidth
        {
            get { return (double)GetValue(MinimumUnitWidthProperty); }
            set { SetValue(MinimumUnitWidthProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinimumUnitWidth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinimumUnitWidthProperty =
            DependencyProperty.Register("MinimumUnitWidth", typeof(double), typeof(TimeLineControl),
                new UIPropertyMetadata(10.0,
                    new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion

        #region snap to grid
        public bool SnapToGrid
        {
            get { return (bool)GetValue(SnapToGridProperty); }
            set { SetValue(SnapToGridProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SnapToGrid.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SnapToGridProperty =
            DependencyProperty.Register("SnapToGrid", typeof(bool), typeof(TimeLineControl),
                new UIPropertyMetadata(null));
        //new UIPropertyMetadata(false,
        //new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion

        #region draw time grid
        public bool DrawTimeGrid
        {
            get { return (bool)GetValue(DrawTimeGridProperty); }
            set { SetValue(DrawTimeGridProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DrawTimeGrid.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DrawTimeGridProperty =
            DependencyProperty.Register("DrawTimeGrid", typeof(bool), typeof(TimeLineControl),
                new UIPropertyMetadata(false,
                    new PropertyChangedCallback(OnDrawTimeGridChanged)));
        #endregion

        #region minor unit thickness
        public int MinorUnitThickness
        {
            get { return (int)GetValue(MinorUnitThicknessProperty); }
            set { SetValue(MinorUnitThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinorUnitThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinorUnitThicknessProperty =
            DependencyProperty.Register("MinorUnitThickness", typeof(int), typeof(TimeLineControl),
                        new UIPropertyMetadata(1,
                            new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion

        #region major unit thickness
        public int MajorUnitThickness
        {
            get { return (int)GetValue(MajorUnitThicknessProperty); }
            set { SetValue(MajorUnitThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MajorUnitThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MajorUnitThicknessProperty =
            DependencyProperty.Register("MajorUnitThickness", typeof(int), typeof(TimeLineControl),
                new UIPropertyMetadata(3, new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion
        private static byte _DefC = 80;

        #region day line brush
        public Brush DayLineBrush
        {
            get { return (Brush)GetValue(DayLineBrushProperty); }
            set { SetValue(DayLineBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DayLineBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DayLineBrushProperty =
            DependencyProperty.Register("DayLineBrush", typeof(Brush), typeof(TimeLineControl),
                new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _DefC, G = _DefC, B = _DefC, A = 255 }),
                    new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion

        #region hour line brush
        public Brush HourLineBrush
        {
            get { return (Brush)GetValue(HourLineBrushProperty); }
            set { SetValue(HourLineBrushProperty, value); }
        }


        // Using a DependencyProperty as the backing store for HourLineBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HourLineBrushProperty =
            DependencyProperty.Register("HourLineBrush", typeof(Brush), typeof(TimeLineControl),
            new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _DefC, G = _DefC, B = _DefC, A = 255 / 2 }),
                new PropertyChangedCallback(OnBackgroundValueChanged)));

        #endregion

        #region minute line brush
        public Brush MinuteLineBrush
        {
            get { return (Brush)GetValue(MinuteLineBrushProperty); }
            set { SetValue(MinuteLineBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinuteLineBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinuteLineBrushProperty =
            DependencyProperty.Register("MinuteLineBrush", typeof(Brush), typeof(TimeLineControl),
            new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _DefC, G = _DefC, B = _DefC, A = 255 / 3 }),
                new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion
        private static void OnDrawTimeGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.DrawBackGround(true);
            }
        }

        private static void OnBackgroundValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.DrawBackGround();
            }
        }
        #endregion

        #region item template
        private DataTemplate _Template;
        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemTemplate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(TimeLineControl),
            new UIPropertyMetadata(null,
                new PropertyChangedCallback(OnItemTemplateChanged)));
        private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.SetTemplate(e.NewValue as DataTemplate);
            }
        }



        #endregion

        #region Items
        public ObservableCollection<EffectBaseVM> Items
        {
            get { return (ObservableCollection<EffectBaseVM>)GetValue(ItemsProperty); }
            set
            {
                Debug.WriteLine("TLC Items set");
                SetValue(ItemsProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for Items.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(ObservableCollection<EffectBaseVM>), typeof(TimeLineControl),
            new UIPropertyMetadata(null,
                new PropertyChangedCallback(OnItemsChanged)));
        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine("TLC OnItemsChanged()");
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.InitializeItems(e.NewValue as ObservableCollection<EffectBaseVM>);
                tc.UpdateUnitSize(tc.UnitSize);
                tc._itemsInitialized = true;

                tc.DrawBackGround();
            }
        }
        #endregion

        #region ViewLevel
        public TimeLineViewLevel ViewLevel
        {
            get { return (TimeLineViewLevel)GetValue(ViewLevelProperty); }
            set { SetValue(ViewLevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ViewLevel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ViewLevelProperty =
            DependencyProperty.Register("ViewLevel", typeof(TimeLineViewLevel), typeof(TimeLineControl),
            new UIPropertyMetadata(TimeLineViewLevel.Hours,
                new PropertyChangedCallback(OnViewLevelChanged)));
        private static void OnViewLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.UpdateViewLevel((TimeLineViewLevel)e.NewValue);
            }
        }
        #endregion

        #region unitsize


        public double UnitSize
        {
            get { return (double)GetValue(UnitSizeProperty); }
            set { SetValue(UnitSizeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for UnitSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UnitSizeProperty =
            DependencyProperty.Register("UnitSize", typeof(double), typeof(TimeLineControl),
            new UIPropertyMetadata(5.0,
                new PropertyChangedCallback(OnUnitSizeChanged)));
        private static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc._unitSizeInitialized = true;
                tc.UpdateUnitSize((double)e.NewValue);
            }
        }



        #endregion

        #region start date
        public DateTime StartDate
        {
            get { return (DateTime)GetValue(StartDateProperty); }
            set { SetValue(StartDateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StartDate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StartDateProperty =
            DependencyProperty.Register("StartDate", typeof(DateTime), typeof(TimeLineControl),
            new UIPropertyMetadata(DateTime.MinValue,
                new PropertyChangedCallback(OnStartDateChanged)));
        private static void OnStartDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc._startDateInitialized = true;
                tc.ReDrawChildren();
            }
        }
        #endregion

        #region manipulation mode
        public TimeLineManipulationMode ManipulationMode
        {
            get { return (TimeLineManipulationMode)GetValue(ManipulationModeProperty); }
            set { SetValue(ManipulationModeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ManipulationMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ManipulationModeProperty =
            DependencyProperty.Register("ManipulationMode", typeof(TimeLineManipulationMode), typeof(TimeLineControl), new UIPropertyMetadata(TimeLineManipulationMode.Free));
        #endregion

        #endregion

        public TimeLineControl()
        {
            _GridCanvas = new Canvas();
            Children.Add(_GridCanvas);
            Focusable = true;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            MouseEnter += TimeLineControl_MouseEnter;
            MouseLeave += TimeLineControl_MouseLeave;
            //Items = new ObservableCollection<ITimeLineDataItem>();

            DragDrop.AddDragOverHandler(this, TimeLineControl_DragOver);
            DragDrop.AddDropHandler(this, TimeLineControl_Drop);
            DragDrop.AddDragEnterHandler(this, TimeLineControl_DragOver);
            DragDrop.AddDragLeaveHandler(this, TimeLineControL_DragLeave);

            AllowDrop = true;

            _ScrollViewer = GetParentScrollViewer();
        }
        #region control life cycle events
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _ScrollViewer = GetParentScrollViewer();
        }


        /*
        /// <summary>
        /// I was unable to track down why this control was locking up when
        /// synchronise with siblings is checked and the parent element is closed etc.
        /// I was getting something with a contextswitchdeadblock that I was wracking my
        /// brain trying to figure out.  The problem only happened when a timeline control
        /// with a child timeline item was present.  I could have n empty timeline controls
        /// with no problem.  Adding one timeline item however caused that error when the parent element
        /// is closed etc.
        /// </summary>
        /// <param name="child"></param>
        protected override void ParentLayoutInvalidated(UIElement child)
        {
            //this event fires when something drags over this or when the control is trying to close
            if (child == _tmpDraggAdornerControl)
                return;
            if (!Children.Contains(child))
                return;
            base.ParentLayoutInvalidated(child);
            SynchedWithSiblings = false;
            //Because this layout invalidated became neccessary, I had to then put null checks on all attempts
            //to get a timeline item control.  There appears to be some UI threading going on so that just checking the children count
            //at the begining of the offending methods was not preventing me from crashing.  
            Children.Clear();
        }*/
        #endregion
        #region miscellaneous helpers
        private ScrollViewer GetParentScrollViewer()
        {
            DependencyObject item = VisualTreeHelper.GetParent(this);
            while (item != null)
            {
                String name = "";
                var ctrl = item as Control;
                if (ctrl != null)
                    name = ctrl.Name;
                if (item is ScrollViewer)
                {
                    return item as ScrollViewer;
                }
                item = VisualTreeHelper.GetParent(item);
            }
            return null;
        }

        private void SetTemplate(DataTemplate dataTemplate)
        {
            _Template = dataTemplate;
            for (int i = 0; i < Children.Count; i++)
            {
                TimeLineItemControl titem = Children[i] as TimeLineItemControl;
                if (titem != null)
                    titem.ContentTemplate = dataTemplate;
            }
        }

        private void InitializeItems(ObservableCollection<EffectBaseVM> observableCollection)
        {
            if (observableCollection == null)
                return;
            this.Children.Clear();
            Children.Add(_GridCanvas);

            foreach (EffectBaseVM data in observableCollection)
            {
                TimeLineItemControl adder = CreateTimeLineItemControl(data);

                Children.Add(adder);
            }
            Items.CollectionChanged -= Items_CollectionChanged;
            Items.CollectionChanged += Items_CollectionChanged;
        }

        void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine("TLC Items_CollectionChanged()");
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                var itm = e.NewItems[0] as EffectBaseVM;
                //if (itm.StartTime.HasValue && itm.StartTime > long.MinValue)
                //{//newly created item isn't a drop in so we need to instantiate and place its control.
                //long duration = itm.EndTime.Value - itm.StartTime.Value;
                //if (Items.Count == 1)//this is the first one added
                //{
                //    itm.StartTime = 0;
                //    itm.EndTime = duration;
                //}
                //else
                //{
                //    var last = Items.OrderBy(i => i.StartTime.Value).LastOrDefault();
                //    if (last != null)
                //    {
                //        itm.StartTime = last.EndTime;
                //        itm.EndTime = itm.StartTime.Value + duration;
                //    }
                //}
                var ctrl = CreateTimeLineItemControl(itm);
                //The index if Items.Count-1 because of zero indexing.
                //however our children is 1 indexed because 0 is our canvas grid.
                Children.Insert(Items.Count, ctrl);
                //}
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                var removeItem = e.OldItems[0];
                for (int i = 1; i < Children.Count; i++)
                {
                    TimeLineItemControl checker = Children[i] as TimeLineItemControl;
                    if (checker != null && checker.DataContext == removeItem)
                    {
                        Children.Remove(checker);
                        break;
                    }
                }
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                // first element is canvas, everything else it TimeLineItemControl
                Children.RemoveRange(1, Children.Count - 1);
            }
        }

        private TimeLineItemControl CreateTimeLineItemControl(EffectBaseVM data)
        {
            Binding startBinding = new Binding("StartFrame");
            startBinding.Mode = BindingMode.TwoWay;
            startBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            startBinding.Converter = new UshortDateConverter();
            Binding endBinding = new Binding("EndFrame");
            endBinding.Mode = BindingMode.TwoWay;
            endBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            endBinding.Converter = new UshortDateConverter();
            DateTime timelineStart = StartDate;

            //Binding expandedBinding = new Binding("TimelineViewExpanded");
            //expandedBinding.Mode = BindingMode.TwoWay;
            endBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;

            TimeLineItemControl adder = new TimeLineItemControl();
            adder.TimeLineStartTime = timelineStart;
            adder.DataContext = data;
            adder.Content = data;

            adder.SetBinding(TimeLineItemControl.StartTimeProperty, startBinding);
            adder.SetBinding(TimeLineItemControl.EndTimeProperty, endBinding);
            //adder.SetBinding(TimeLineItemControl.IsExpandedProperty, expandedBinding);

            if (_Template != null)
            {
                adder.ContentTemplate = _Template;
            }

            /*adder.PreviewMouseLeftButtonDown += item_PreviewEditButtonDown;
            adder.MouseMove += item_MouseMove;
            adder.PreviewMouseLeftButtonUp += item_PreviewEditButtonUp;*/
            adder.PreviewMouseRightButtonDown += Item_PreviewEditButtonDown;
            adder.MouseMove += Item_MouseMove;
            adder.PreviewMouseRightButtonUp += Item_PreviewEditButtonUp;

            adder.PreviewMouseLeftButtonUp += Item_PreviewDragButtonUp;
            adder.PreviewMouseLeftButtonDown += Item_PreviewDragButtonDown;
            adder.UnitSize = UnitSize;
            return adder;
        }
        #endregion

        #region updaters fired on dp changes
        private void UpdateUnitSize(double size)
        {
            if (Items == null)
                return;
            for (int i = 0; i < Items.Count; i++)
            {
                TimeLineItemControl titem = GetTimeLineItemControlAt(i);
                if (titem != null)
                    titem.UnitSize = size;
            }
            ReDrawChildren();
        }
        private void UpdateViewLevel(TimeLineViewLevel lvl)
        {
            if (Items == null)
                return;
            for (int i = 0; i < Items.Count; i++)
            {
                var templatedControl = GetTimeLineItemControlAt(i);
                if (templatedControl != null)
                    templatedControl.ViewLevel = lvl;
            }
            ReDrawChildren();
            //Now we go back and have to detect if things have been collapsed
        }

        //TODO: set up the timeline start date dependency property and do this margin check
        //for all including the first one.
        private void ReDrawChildren()
        {
            if (Items == null)
            {
                DrawBackGround();
                return;
            }
            DateTime start = (DateTime)GetValue(StartDateProperty);
            double w = 0;
            double s = 0;
            double e = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                var mover = GetTimeLineItemControlAt(i);
                if (mover != null)
                {
                    mover.TimeLineStartTime = start;
                    if (!mover.ReadyToDraw)
                        mover.ReadyToDraw = true;
                    mover.PlaceOnCanvas();
                    mover.GetPlacementInfo(ref s, ref w, ref e);
                }
            }
            //find our background rectangle and set its width;
            DrawBackGround();
        }
        #endregion

        #region background and grid methods
        private void DrawBackGround(bool isDrawGridUpdate = false)
        {
            Brush b = Background;
            double setWidth = MinWidth;
            if (_GridCanvas.Children.Count <= 0)
            {
                _GridCanvas.Children.Add(new System.Windows.Shapes.Rectangle());
            }
            System.Windows.Shapes.Rectangle bg = _GridCanvas.Children[0] as System.Windows.Shapes.Rectangle;
            if (!_startDateInitialized ||
                !_unitSizeInitialized ||
                !_itemsInitialized ||
                Items == null)
            {
                setWidth = Math.Max(MinWidth, GetMyWidth());
                setWidth = Math.Max(setWidth, SynchWidth);
                bg.Width = setWidth;
                bg.Height = Math.Max(DesiredSize.Height, Height);
                if (double.IsNaN(bg.Height) || bg.Height < MinHeight)
                {
                    bg.Height = MinHeight;
                }
                bg.Fill = b;
                Width = bg.Width;
                Height = bg.Height;
            }
            else
            {
                var oldW = Width;
                var oldDrawTimeGrid = DrawTimeGrid;
                if (isDrawGridUpdate)
                    oldDrawTimeGrid = !oldDrawTimeGrid;
                //this is run every time we may need to update our siblings.
                SynchronizeSiblings();



                if (Items == null)
                    return;
                setWidth = Math.Max(MinWidth, GetMyWidth());
                setWidth = Math.Max(setWidth, SynchWidth);
                bg.Width = setWidth;
                bg.Height = Math.Max(DesiredSize.Height, Height);
                if (double.IsNaN(bg.Height) || bg.Height < MinHeight)
                {
                    bg.Height = MinHeight;
                }
                bg.Fill = b;
                Width = bg.Width;
                Height = bg.Height;
                if (DrawTimeGrid)
                {
                    if (Width != oldW || !oldDrawTimeGrid || (Width == MinWidth))
                        DrawTimeGridExecute();
                }
                else
                {
                    ClearTimeGridExecute();
                }
                if ((oldW != Width) && (_ScrollViewer != null))//if we are at min width then we need to redraw our time grid when unit sizes change
                {
                    var available = LayoutInformation.GetLayoutSlot(_ScrollViewer);
                    Size s = new Size(available.Width, available.Height);
                    _ScrollViewer.Measure(s);
                    _ScrollViewer.Arrange(available);
                }
            }

        }

        internal double GetMyWidth()
        {
            //if (Items == null)
            //{
            //    return MinWidth;
            //}
            //var lastItem = GetTimeLineItemControlAt(Items.Count - 1);

            //if (lastItem == null)
            //    return MinWidth;
            //double l = 0;
            //double w = 0;
            //double e = 0;
            //lastItem.GetPlacementInfo(ref l, ref w, ref e);
            //return Math.Max(MinWidth, e);
            return ConvertTotalFramesToDistance();
        }

        // based on ConvertTimeToDistance in TimeLineItemControl
        private double ConvertTotalFramesToDistance()
        {
            ushort totalFrames = (ushort)GetValue(TotalFramesProperty);
            TimeSpan span = TimeSpan.FromMinutes(totalFrames);
            TimeLineViewLevel lvl = (TimeLineViewLevel)GetValue(ViewLevelProperty);
            double unitSize = (double)GetValue(UnitSizeProperty);
            double value = unitSize;
            switch (lvl)
            {
                case TimeLineViewLevel.Minutes:
                    value = span.TotalMinutes * unitSize;
                    break;
                case TimeLineViewLevel.Hours:
                    value = span.TotalHours * unitSize;
                    break;
                case TimeLineViewLevel.Days:
                    value = span.TotalDays * unitSize;
                    break;
                case TimeLineViewLevel.Weeks:
                    value = (span.TotalDays / 7.0) * unitSize;
                    break;
                case TimeLineViewLevel.Months:
                    value = (span.TotalDays / 30.0) * unitSize;
                    break;
                case TimeLineViewLevel.Years:
                    value = (span.TotalDays / 365.0) * unitSize;
                    break;
                default:
                    break;
            }
            return value;
        }

        private void SynchronizeSiblings()
        {
            if (!SynchedWithSiblings)
                return;
            var current = VisualTreeHelper.GetParent(this) as FrameworkElement;

            while (current != null && !(current is ItemsControl))
            {
                current = VisualTreeHelper.GetParent(current) as FrameworkElement;
            }

            if (current is ItemsControl)
            {
                var pnl = current as ItemsControl;
                //this is called on updates for all siblings so it could easily
                //end up infinitely looping if each time tried to synch its siblings
                bool isSynchInProgress = false;
                //is there a synch instigator
                double maxWidth = GetMyWidth();

                var siblings = TimeLineControl.FindAllTimeLineControlsInsidePanel(current);

                foreach (var ctrl in siblings)
                {
                    var tcSib = ctrl as TimeLineControl;
                    if (tcSib != null)
                    {
                        if (tcSib._isSynchInstigator)
                            isSynchInProgress = true;
                        double sibW = tcSib.GetMyWidth();
                        if (sibW > maxWidth)
                        {
                            maxWidth = sibW;
                        }
                    }
                }
                SynchWidth = maxWidth;
                if (!isSynchInProgress)
                {
                    _isSynchInstigator = true;
                    foreach (var ctrl in siblings)
                    {
                        var tcSib = ctrl as TimeLineControl;
                        if (tcSib != null && tcSib != this)
                        {
                            tcSib.SynchWidth = maxWidth;
                            //tcSib.UnitSize = UnitSize;
                            //tcSib.StartDate = StartDate;
                            tcSib.DrawBackGround();
                        }
                    }
                }
                _isSynchInstigator = false;
            }
        }

        //helper to let a panel find all children of a given type
        private static IEnumerable<TimeLineControl> FindAllTimeLineControlsInsidePanel(DependencyObject depObj)
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is TimeLineControl)
                    {
                        yield return (TimeLineControl)child;
                    }

                    foreach (TimeLineControl childOfChild in FindAllTimeLineControlsInsidePanel(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void ClearTimeGridExecute()
        {
            if (_GridCanvas.Children.Count == 2)
                _GridCanvas.Children.RemoveAt(1);
        }

        private void DrawTimeGridExecute()
        {
            if (Items == null)
                return;
            if (StartDate == DateTime.MinValue)
                return;
            if (_GridCanvas.Children.Count < 2)
            {
                _GridCanvas.Children.Add(new Canvas());
            }
            Canvas grid = _GridCanvas.Children[1] as Canvas;
            grid.Children.Clear();
            double hourSize = UnitSize;

            //place our gridlines
            DrawDayLines(grid);
            DrawHourLines(grid);
            DrawMinuteLines(grid);
        }

        private void DrawMinuteLines(Canvas grid)
        {
            double halfHourSize = UnitSize / 2;
            double fifteenMinSize = UnitSize / 4;
            double minuteSize = UnitSize / 60;
            int startMinute = StartDate.Minute;
            int startSecond = StartDate.Second;
            int remainingMinutes = 60 - startMinute;
            int remainingSeconds = 60 - startSecond;
            if (remainingSeconds == 60)
                remainingSeconds = 0;


            if (fifteenMinSize >= MinimumUnitWidth)
            {
                if (startMinute < 45)
                    remainingMinutes = 45 - startMinute;
                if (startMinute < 30)
                    remainingMinutes = 30 - startMinute;
                if (startMinute < 15)
                    remainingMinutes = 15 - startMinute;
                if (startSecond != 0)
                    remainingMinutes--;
                else remainingMinutes = 0;

                TimeSpan nextFifteenGap = new TimeSpan(0, remainingMinutes, remainingSeconds);
                DateTime nextFifteenDate = StartDate.Add(nextFifteenGap);
                double nextFifteenDistance = nextFifteenGap.TotalHours * UnitSize;
                DrawIncrementLines(grid, nextFifteenDate, nextFifteenDistance, new TimeSpan(0, 15, 0), fifteenMinSize, MinuteLineBrush, 0);
            }
            else if (halfHourSize >= MinimumUnitWidth)
            {
                if (startMinute < 30)
                    remainingMinutes = 30 - startMinute;
                if (startSecond != 0)
                    remainingMinutes--;
                TimeSpan nextHalfGap = new TimeSpan(0, remainingMinutes, remainingSeconds);
                DateTime nextHalfDate = StartDate.Add(nextHalfGap);
                double nextHalfDistance = nextHalfGap.TotalHours * UnitSize;
                DrawIncrementLines(grid, nextHalfDate, nextHalfDistance, new TimeSpan(0, 30, 0), halfHourSize, MinuteLineBrush, 0);
            }
        }

        private void DrawHourLines(Canvas grid)
        {
            double hourSize = UnitSize;
            double halfDaySize = hourSize * 12;
            int startMinute = StartDate.Minute;
            int remainingMinutes = 60 - startMinute;
            int startSecond = StartDate.Second;
            int remainingSeconds = 60 - startSecond;
            if (remainingSeconds == 60)
                remainingSeconds = 0;
            if (startSecond != 0)
                remainingMinutes--;
            if (startSecond != 0)
                remainingMinutes--;
            else remainingMinutes = 0;

            if (hourSize >= MinimumUnitWidth)
            {
                int remainingToMajor = 24 - StartDate.Hour;
                if (StartDate.Hour < 12)
                    remainingToMajor = 12 - StartDate.Hour;
                //time to our next hour
                TimeSpan firstHourGap = new TimeSpan(0, remainingMinutes, remainingSeconds);
                DateTime nextHour = StartDate.Add(firstHourGap);
                double firstHourDistance = firstHourGap.TotalHours * hourSize;
                DrawIncrementLines(grid, nextHour, firstHourDistance,
                                    new TimeSpan(1, 0, 0), hourSize, HourLineBrush, 12, remainingToMajor);
            }
            else if (halfDaySize >= MinimumUnitWidth)
            {
                int startHour = StartDate.Hour;
                int remainingHours = 24 - startHour;
                if (startHour < 12)
                {
                    remainingHours = 12 - startHour;
                }
                if (startMinute != 0)
                    remainingHours--;


                TimeSpan nextHalfGap = new TimeSpan(remainingHours, remainingMinutes, remainingSeconds);
                DateTime nextHalfDay = StartDate.Add(nextHalfGap);
                double nextHalfDistance = nextHalfGap.TotalHours * hourSize;
                DrawIncrementLines(grid, nextHalfDay, nextHalfDistance, new TimeSpan(12, 0, 0), halfDaySize, HourLineBrush, -1);
            }
        }

        private void DrawDayLines(Canvas grid)
        {
            double daySize = UnitSize * 24;


            if (daySize >= MinimumUnitWidth)
            {
                TimeSpan increment = new TimeSpan(24, 0, 0);
                int startHour = StartDate.Hour;
                int startMinute = StartDate.Minute;
                int remainingHours = 24 - startHour;
                if (startMinute > 0)
                    remainingHours--;
                int remainingMinutes = 60 - startMinute;
                if (startMinute == 0)
                    remainingMinutes = 0;
                int startSecond = StartDate.Second;
                int remainingSeconds = 60 - startSecond;
                if (startSecond != 0)
                    remainingMinutes--;
                else
                    remainingSeconds = 0;


                TimeSpan firstDayGap = new TimeSpan(remainingHours, remainingMinutes, remainingSeconds);
                double firstDayDistance = (firstDayGap.TotalHours * UnitSize);
                DateTime nextDay = StartDate.Add(new TimeSpan(remainingHours, remainingMinutes, 0));


                DrawIncrementLines(grid, nextDay, firstDayDistance,
                                    increment, daySize, DayLineBrush, 7);
            }
        }

        private void DrawIncrementLines(Canvas grid, DateTime firstLineDate, double firstLineDistance,
                TimeSpan timeStep, double unitSize, Brush brush, int majorEvery, int majorEveryOffset = 0)
        {
            double curX = firstLineDistance;
            DateTime curDate = firstLineDate;
            int curLine = 0;
            while (curX < Width)
            {
                Line l = new Line();
                l.ToolTip = curDate;
                l.StrokeThickness = MinorUnitThickness;
                if ((majorEvery > 0) && ((curLine - majorEveryOffset) % majorEvery == 0))
                {
                    l.StrokeThickness = MajorUnitThickness;
                }
                l.Stroke = brush;
                l.X1 = 0;
                l.X2 = 0;
                l.Y1 = 0;
                l.Y2 = Math.Max(DesiredSize.Height, Height);
                grid.Children.Add(l);
                Canvas.SetLeft(l, curX);
                curX += unitSize;
                curDate += timeStep;
                curLine++;
            }
        }
        #endregion

        #region mouse enter and leave events
        void TimeLineControl_MouseLeave(object sender, MouseEventArgs e)
        {
            //Keyboard.Focus(this);
        }

        void TimeLineControl_MouseEnter(object sender, MouseEventArgs e)
        {
            //Keyboard.Focus(this);
        }
        #endregion

        #region drag events and fields
        private bool _Dragging = false;
        private Point _DragStartPosition = new Point(double.MinValue, double.MinValue);
        /// <summary>
        /// When we drag something from an external control over this I need a temp control
        /// that lets me adorn those accordingly as well
        /// </summary>
        private TimeLineItemControl _TmpDragAdornerControl;

        TimeLineItemControl _DragObject = null;
        void Item_PreviewDragButtonDown(object sender, MouseButtonEventArgs e)
        {
            _DragStartPosition = Mouse.GetPosition(null);
            _DragObject = sender as TimeLineItemControl;

            // send message about selected effect
            var data = new MediatorMessageData.TimeLineEffectSelectedData((EffectBaseVM)_DragObject.Content);
            App.Instance.MediatorService.BroadcastMessage(MediatorMessages.TimeLineEffectSelected, this, data);
        }

        void Item_PreviewDragButtonUp(object sender, MouseButtonEventArgs e)
        {
            _DragStartPosition.X = double.MinValue;
            _DragStartPosition.Y = double.MinValue;
            _DragObject = null;
        }


        void TimeLineControl_DragOver(object sender, DragEventArgs e)
        {
            //throw new NotImplementedException();
            TimeLineItemControl d = e.Data.GetData(typeof(TimeLineItemControl)) as TimeLineItemControl;
            if (d != null)
            {
                e.Effects = DragDropEffects.Move;
                //this is an internal drag or a drag from another time line control
                if (DragAdorner == null)
                {
                    _dragAdorner = new TimeLineDragAdorner(d, ItemTemplate);

                }
                DragAdorner.MousePosition = e.GetPosition(d);
                DragAdorner.InvalidateVisual();
            }
            else
            {//GongSolutions.Wpf.DragDrop
                var d2 = e.Data.GetData("GongSolutions.Wpf.DragDrop");
                if (d2 != null)
                {
                    e.Effects = DragDropEffects.Move;
                    if (DragAdorner == null)
                    {
                        //we are dragging from an external source and we don't have a timeline item control of any sort
                        Children.Remove(_TmpDragAdornerControl);
                        //in order to get an adornment layer the control has to be somewhere
                        _TmpDragAdornerControl = new TimeLineItemControl();
                        _TmpDragAdornerControl.UnitSize = UnitSize;
                        Children.Add(_TmpDragAdornerControl);
                        Canvas.SetLeft(_TmpDragAdornerControl, -1000000);
                        _TmpDragAdornerControl.DataContext = d2;
                        _TmpDragAdornerControl.StartTime = StartDate;
                        _TmpDragAdornerControl.InitializeDefaultLength();
                        _TmpDragAdornerControl.ContentTemplate = ItemTemplate;

                        _dragAdorner = new TimeLineDragAdorner(_TmpDragAdornerControl, ItemTemplate);
                    }
                    DragAdorner.MousePosition = e.GetPosition(_TmpDragAdornerControl);
                    DragAdorner.InvalidateVisual();
                }
            }
            DragScroll(e);
        }

        void TimeLineControL_DragLeave(object sender, DragEventArgs e)
        {
            DragAdorner = null;
            Children.Remove(_TmpDragAdornerControl);
            _TmpDragAdornerControl = null;
        }

        void TimeLineControl_Drop(object sender, DragEventArgs e)
        {
            DragAdorner = null;

            TimeLineItemControl dropper = e.Data.GetData(typeof(TimeLineItemControl)) as TimeLineItemControl;
            EffectBaseVM dropData = null;
            if (dropper == null)
            {
                //dropData = e.Data.GetData(typeof(ITimeLineDataItem)) as ITimeLineDataItem;
                dropData = e.Data.GetData("GongSolutions.Wpf.DragDrop") as EffectBaseVM;
                if (dropData != null)
                {
                    //I haven't figured out why but
                    //sometimes when dropping from an external source
                    //the drop event hits twice.
                    //that results in ugly duplicates ending up in the timeline
                    //and it is a mess.
                    if (Items.Contains(dropData))
                        return;
                    //create a new timeline item control from this data
                    dropper = CreateTimeLineItemControl(dropData);
                    dropper.StartTime = StartDate;
                    dropper.InitializeDefaultLength();
                    Children.Remove(_TmpDragAdornerControl);
                    _TmpDragAdornerControl = null;

                }
            }
            var dropX = e.GetPosition(this).X;
            int newIndex = GetDroppedNewIndex(dropX);
            var curData = dropper.DataContext as EffectBaseVM;
            var curIndex = Items.IndexOf(curData);
            if ((curIndex == newIndex || curIndex + 1 == newIndex) && dropData == null && dropper.Parent == this)//dropdata null is to make sure we aren't failing on adding a new data item into the timeline
            //dropper.parent==this makes it so that we allow a dropper control from another timeline to be inserted in at the start.
            {
                return;//our drag did nothing meaningful so we do nothing.
            }

            if (dropper != null)
            {
                DateTime start = (DateTime)GetValue(StartDateProperty);
                if (newIndex == 0)
                {
                    if (dropData == null)
                    {
                        RemoveTimeLineItemControl(dropper);
                    }
                    if (dropper.Parent != this && dropper.Parent is TimeLineControl)
                    {
                        var tlCtrl = dropper.Parent as TimeLineControl;
                        tlCtrl.RemoveTimeLineItemControl(dropper);
                    }
                    InsertTimeLineItemControlAt(newIndex, dropper);
                    dropper.MoveToNewStartTime(start);
                    MakeRoom(newIndex, dropper.Width);
                }
                else//we are moving this after something.
                {
                    //find out if we are moving the existing one back or forward.
                    var placeAfter = GetTimeLineItemControlAt(newIndex - 1);
                    if (placeAfter != null)
                    {
                        start = placeAfter.EndTime;
                        RemoveTimeLineItemControl(dropper);
                        if (curIndex < newIndex && curIndex >= 0)//-1 is on an insert in which case we definitely don't want to take off on our new index value
                        {
                            //we are moving forward.
                            newIndex--;//when we removed our item, we shifted our insert index back 1
                        }
                        if (dropper.Parent != null && dropper.Parent != this)
                        {
                            var ptl = dropper.Parent as TimeLineControl;
                            ptl.RemoveTimeLineItemControl(dropper);
                        }

                        InsertTimeLineItemControlAt(newIndex, dropper);
                        dropper.MoveToNewStartTime(start);
                        MakeRoom(newIndex, dropper.Width);
                    }
                }
            }
            //ReDrawChildren();
            DrawBackGround();
            e.Handled = true;
        }


        #region drop helpers
        private void InsertTimeLineItemControlAt(int index, TimeLineItemControl adder)
        {
            var Data = adder.DataContext as EffectBaseVM;
            if (Items.Contains(Data))
                return;

            adder.PreviewMouseRightButtonDown -= Item_PreviewEditButtonDown;
            adder.MouseMove -= Item_MouseMove;
            adder.PreviewMouseRightButtonUp -= Item_PreviewEditButtonUp;

            adder.PreviewMouseLeftButtonUp -= Item_PreviewDragButtonUp;
            adder.PreviewMouseLeftButtonDown -= Item_PreviewDragButtonDown;

            adder.PreviewMouseRightButtonDown += Item_PreviewEditButtonDown;
            adder.MouseMove += Item_MouseMove;
            adder.PreviewMouseRightButtonUp += Item_PreviewEditButtonUp;

            adder.PreviewMouseLeftButtonUp += Item_PreviewDragButtonUp;
            adder.PreviewMouseLeftButtonDown += Item_PreviewDragButtonDown;
            //child 0 is our grid and we want to keep that there.
            Children.Insert(index + 1, adder);
            Items.Insert(index, Data);
        }

        private void RemoveTimeLineItemControl(TimeLineItemControl remover)
        {
            var curData = remover.DataContext as EffectBaseVM;
            remover.PreviewMouseRightButtonDown -= Item_PreviewEditButtonDown;
            remover.MouseMove -= Item_MouseMove;
            remover.PreviewMouseRightButtonUp -= Item_PreviewEditButtonUp;

            remover.PreviewMouseLeftButtonUp -= Item_PreviewDragButtonUp;
            remover.PreviewMouseLeftButtonDown -= Item_PreviewDragButtonDown;
            Items.Remove(curData);
            Children.Remove(remover);
        }

        private int GetDroppedNewIndex(double dropX)
        {
            double s = 0;
            double w = 0;
            double e = 0;
            for (int i = 0; i < Items.Count(); i++)
            {
                var checker = GetTimeLineItemControlAt(i);
                if (checker == null)
                    continue;
                checker.GetPlacementInfo(ref s, ref w, ref e);
                if (dropX < s)
                {
                    return i;
                }
                if (s < dropX && e > dropX)
                {
                    double distStart = Math.Abs(dropX - s);
                    double distEnd = Math.Abs(dropX - e);
                    if (distStart < distEnd)//we dropped closer to the start of this item
                    {
                        return i;
                    }
                    //we are closer to the end of this item
                    return i + 1;
                }
                if (e < dropX && i == Items.Count() - 1)
                {
                    return i + 1;
                }
                if (s < dropX && i == Items.Count() - 1)
                {
                    return i;
                }
            }
            return Items.Count;
        }

        private void MakeRoom(int newIndex, double width)
        {
            int moveIndex = newIndex + 1;
            //get our forward chain and gap
            double chainGap = 0;

            //because the grid is child 0 and we are essentially indexing as if it wasn't there
            //the child index of add after is our effective index of next
            var nextCtrl = GetTimeLineItemControlAt(moveIndex);
            if (nextCtrl != null)
            {
                double nL = 0;
                double nW = 0;
                double nE = 0;
                nextCtrl.GetPlacementInfo(ref nL, ref nW, ref nE);

                double droppedIntoSpace = 0;
                if (newIndex == 0)
                {
                    droppedIntoSpace = nL;
                }
                else
                {
                    var previousControl = GetTimeLineItemControlAt(newIndex - 1);
                    if (previousControl != null)
                    {
                        double aL = 0;
                        double aW = 0;
                        double aE = 0;
                        previousControl.GetPlacementInfo(ref aL, ref aW, ref aE);
                        droppedIntoSpace = nL - aE;
                    }
                }
                double neededSpace = width - droppedIntoSpace;
                if (neededSpace <= 0)
                    return;


                bool lastHitsTotalFrames = false; // TODO dropping won't work near the end until this is properly implemented, I guess
                var forwardChain = GetTimeLineForwardChain(nextCtrl, moveIndex + 1, ref lastHitsTotalFrames, ref chainGap);

                if (chainGap < neededSpace)
                {
                    while (neededSpace > 0)
                    {
                        //move it to the smaller of our values -gap or remaning space
                        double move = Math.Min(chainGap, neededSpace);
                        foreach (var tictrl in forwardChain)
                        {
                            tictrl.MoveMe(move);
                            neededSpace -= move;
                        }
                        //get our new chain and new gap
                        forwardChain = GetTimeLineForwardChain(nextCtrl, moveIndex + 1, ref lastHitsTotalFrames, ref chainGap);
                    }
                }
                else
                {
                    foreach (var tictrl in forwardChain)
                    {
                        tictrl.MoveMe(neededSpace);
                    }
                }
            }//if next ctrl is null we are adding to the very end and there is no work to do to make room.
        }
        #endregion




        //NOT WORKING YET AND I DON'T KNOW WHY 8(
        private void DragScroll(DragEventArgs e)
        {
            if (_ScrollViewer == null)
            {
                _ScrollViewer = GetParentScrollViewer();
            }
            if (_ScrollViewer != null)
            {
                var available = LayoutInformation.GetLayoutSlot(this);
                Point scrollPos = e.GetPosition(_ScrollViewer);
                double scrollMargin = 50;
                var actualW = _ScrollViewer.ActualWidth;
                if (scrollPos.X >= actualW - scrollMargin &&
                    _ScrollViewer.HorizontalOffset <= _ScrollViewer.ExtentWidth - _ScrollViewer.ViewportWidth)
                {
                    _ScrollViewer.LineRight();
                }
                else if (scrollPos.X < scrollMargin && _ScrollViewer.HorizontalOffset > 0)
                {
                    _ScrollViewer.LineLeft();
                }
                double actualH = _ScrollViewer.ActualHeight;
                if (scrollPos.Y >= actualH - scrollMargin &&
                    _ScrollViewer.VerticalOffset <= _ScrollViewer.ExtentHeight - _ScrollViewer.ViewportHeight)
                {
                    _ScrollViewer.LineDown();
                }
                else if (scrollPos.Y < scrollMargin && _ScrollViewer.VerticalOffset >= 0)
                {
                    _ScrollViewer.LineUp();
                }
            }
        }



        #endregion


        #region edit events etc
        private double _CurX = 0;
        private TimeLineAction _action;
        void Item_PreviewEditButtonUp(object sender, MouseButtonEventArgs e)
        {
            (sender as TimeLineItemControl).ReleaseMouseCapture();
            Keyboard.Focus(this);
        }

        void Item_PreviewEditButtonDown(object sender, MouseButtonEventArgs e)
        {
            var ctrl = sender as TimeLineItemControl;

            _action = ctrl.GetClickAction();
            (sender as TimeLineItemControl).CaptureMouse();
        }



        #region key down and up
        bool _RightCtrlDown = false;
        bool _LeftCtrlDown = false;
        protected void OnKeyDown(object sender, KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _RightCtrlDown = e.Key == Key.RightCtrl;
                _LeftCtrlDown = e.Key == Key.LeftCtrl;
                ManipulationMode = TimeLineManipulationMode.Linked;
            }
        }
        protected void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
                _LeftCtrlDown = false;
            if (e.Key == Key.RightCtrl)
                _RightCtrlDown = false;
            if (!_LeftCtrlDown && !_RightCtrlDown)
                ManipulationMode = TimeLineManipulationMode.Linked;
        }

        internal void HandleItemManipulation(TimeLineItemControl ctrl, TimeLineItemChangedEventArgs e)
        {
            bool doStretch = false;
            TimeSpan deltaT = e.DeltaTime;
            TimeSpan zeroT = new TimeSpan();
            int direction = deltaT.CompareTo(zeroT);
            if (direction == 0)
                return;//shouldn't happen

            TimeLineItemControl previous = null;
            TimeLineItemControl after = null;
            int afterIndex = -1;
            int previousIndex = -1;
            after = GetTimeLineItemControlStartingAfter(ctrl.StartTime, ref afterIndex);
            previous = GetTimeLineItemControlStartingBefore(ctrl.StartTime, ref previousIndex);
            if (after != null)
                after.ReadyToDraw = false;
            if (ctrl != null)
                ctrl.ReadyToDraw = false;
            double useDeltaX = e.DeltaX;
            double cLeft = 0;
            double cWidth = 0;
            double cEnd = 0;
            ctrl.GetPlacementInfo(ref cLeft, ref cWidth, ref cEnd);

            switch (e.Action)
            {
                case TimeLineAction.Move:
                    #region move

                    double chainGap = double.MaxValue;
                    if (direction > 0)
                    {
                        //find chain connecteds that are after this one
                        //delta each one in that chain that we are pushing
                        bool lastHitsTotalFrames = false;
                        List<TimeLineItemControl> afterChain = GetTimeLineForwardChain(ctrl, afterIndex, ref lastHitsTotalFrames, ref chainGap);
                        Console.WriteLine("to end? " + lastHitsTotalFrames);

                        if (chainGap < useDeltaX)
                        {
                            useDeltaX = chainGap;
                        }
                        if (!lastHitsTotalFrames)
                        {
                            foreach (var ti in afterChain)
                            {
                                ti.MoveMe(useDeltaX);
                            }
                        }

                        //find the size of our chain so we bring it into view
                        var first = afterChain[0];
                        var last = afterChain[afterChain.Count - 1];
                        BringChainIntoView(first, last, direction);
                    }
                    if (direction < 0)
                    {
                        bool previousBackToStart = false;
                        List<TimeLineItemControl> previousChain = GetTimeLineBackwardsChain(ctrl, previousIndex, ref previousBackToStart, ref chainGap);
                        if (-chainGap > useDeltaX)
                        {
                            useDeltaX = chainGap;
                        }
                        if (!previousBackToStart)
                        {
                            foreach (var ti in previousChain)
                            {
                                ti.MoveMe(useDeltaX);
                            }
                        }
                        var first = previousChain[0];//previousChain[previousChain.Count - 1];
                        var last = previousChain[previousChain.Count - 1];
                        BringChainIntoView(last, first, direction);
                    }
                    #endregion
                    break;
                case TimeLineAction.StretchStart:
                    switch (e.Mode)
                    {
                        #region stretchstart

                        case TimeLineManipulationMode.Linked:
                            #region linked
                            double gap = double.MaxValue;
                            if (previous != null)
                            {
                                double pLeft = 0;
                                double pWidth = 0;
                                double pEnd = 0;
                                previous.GetPlacementInfo(ref pLeft, ref pWidth, ref pEnd);
                                gap = cLeft - pEnd;
                            }
                            if (direction < 0 && Math.Abs(gap) < Math.Abs(useDeltaX) && Math.Abs(gap) > _BumpThreshold)//if we are negative and not linked, but about to bump
                                useDeltaX = -gap;
                            if (Math.Abs(gap) < _BumpThreshold)
                            {//we are linked
                                if (ctrl.CanDelta(0, useDeltaX) && previous.CanDelta(1, useDeltaX))
                                {
                                    ctrl.MoveStartTime(useDeltaX);
                                    previous.MoveEndTime(useDeltaX);
                                }
                            }
                            else if (ctrl.CanDelta(0, useDeltaX))
                            {
                                ctrl.MoveStartTime(useDeltaX);
                            }


                            break;
                        #endregion
                        case TimeLineManipulationMode.Free:
                            #region free
                            gap = double.MaxValue;
                            doStretch = direction > 0;
                            if (direction < 0)
                            {
                                //disallow us from free stretching into another item

                                if (previous != null)
                                {
                                    double pLeft = 0;
                                    double pWidth = 0;
                                    double pEnd = 0;
                                    previous.GetPlacementInfo(ref pLeft, ref pWidth, ref pEnd);
                                    gap = cLeft - pEnd;
                                }

                                else
                                {
                                    //don't allow us to stretch further than the gap between current and start time
                                    DateTime s = (DateTime)GetValue(StartDateProperty);
                                    gap = cLeft;
                                }
                                doStretch = gap > _BumpThreshold;
                                if (gap < useDeltaX)
                                {
                                    useDeltaX = gap;
                                }
                            }

                            doStretch &= ctrl.CanDelta(0, useDeltaX);

                            if (doStretch)
                            {
                                ctrl.MoveStartTime(useDeltaX);
                            }
                            #endregion
                            break;
                        default:
                            break;
                            #endregion
                    }
                    break;
                case TimeLineAction.StretchEnd:
                    switch (e.Mode)
                    {
                        #region stretchend
                        case TimeLineManipulationMode.Linked:
                            #region linked
                            double gap = double.MaxValue;
                            if (after != null)
                            {
                                double aLeft = 0;
                                double aWidth = 0;
                                double aEnd = 0;
                                after.GetPlacementInfo(ref aLeft, ref aWidth, ref aEnd);
                                gap = aLeft - cEnd;
                            }

                            if (direction > 0 && gap > _BumpThreshold && gap < useDeltaX)//if we are positive, not linked but about to bump
                                useDeltaX = -gap;
                            if (gap < _BumpThreshold)
                            {//we are linked
                                if (ctrl.CanDelta(1, useDeltaX) && after.CanDelta(0, useDeltaX))
                                {
                                    ctrl.MoveEndTime(useDeltaX);
                                    after.MoveStartTime(useDeltaX);
                                }
                            }
                            else if (ctrl.CanDelta(0, useDeltaX))
                            {
                                ctrl.MoveEndTime(useDeltaX);
                            }
                            break;
                        #endregion
                        case TimeLineManipulationMode.Free:
                            #region free
                            double nextGap = double.MaxValue;
                            doStretch = true;
                            if (direction > 0 && after != null)
                            {
                                //disallow us from free stretching into another item
                                double nLeft = 0;
                                double nWidth = 0;
                                double nEnd = 0;
                                after.GetPlacementInfo(ref nLeft, ref nWidth, ref nEnd);
                                nextGap = nLeft - cEnd;
                                doStretch = nextGap > _BumpThreshold;
                                if (nextGap < useDeltaX)
                                    useDeltaX = nextGap;
                            }


                            doStretch &= ctrl.CanDelta(1, useDeltaX);
                            if (doStretch)
                            {
                                ctrl.MoveEndTime(useDeltaX);
                            }

                            break;
                        #endregion
                        default:
                            break;
                            #endregion
                    }
                    break;
                default:
                    break;
            }
        }

        private void BringChainIntoView(TimeLineItemControl first, TimeLineItemControl last, int direction)
        {
            double l1 = 0;
            double l2 = 0;
            double w = 0;
            double w2 = 0;
            double end = 0;
            first.GetPlacementInfo(ref l1, ref w, ref end);
            last.GetPlacementInfo(ref l2, ref w2, ref end);
            double chainW = end - l1;
            double leadBuffer = 4 * UnitSize;
            chainW += leadBuffer;
            if (direction > 0)
            {

                first.BringIntoView(new Rect(new Point(0, 0), new Point(chainW, Height)));
            }
            else
            {
                first.BringIntoView(new Rect(new Point(-leadBuffer, 0), new Point(chainW, Height)));
            }

        }

        #endregion
        #endregion

        /// <summary>
        /// Mouse move is important for both edit and drag behaviors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Item_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            #region drag - left click and move
            TimeLineItemControl ctrl = sender as TimeLineItemControl;
            if (ctrl == null)
                return;

            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                if (ctrl.IsExpanded)
                    return;
                var position = Mouse.GetPosition(null);
                if (Math.Abs(position.X - _DragStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _DragStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(this, ctrl, DragDropEffects.Move | DragDropEffects.Scroll);
                    _Dragging = true;


                }

                return;
            }
            #endregion


            #region edits - right click and move
            if (Mouse.Captured != ctrl)
            {
                _CurX = Mouse.GetPosition(null).X;
                return;
            }

            double mouseX = Mouse.GetPosition(null).X;
            double deltaX = mouseX - _CurX;
            TimeSpan deltaT = ctrl.GetDeltaTime(deltaX);
            var curMode = (TimeLineManipulationMode)GetValue(ManipulationModeProperty);
            HandleItemManipulation(ctrl, new TimeLineItemChangedEventArgs()
            {
                Action = _action,
                DeltaTime = deltaT,
                DeltaX = deltaX,
                Mode = curMode
            });

            DrawBackGround();
            _CurX = mouseX;

            //When we pressed, this lost focus and we therefore didn't capture any changes to the key status
            //so we check it again after our manipulation finishes.  That way we can be linked and go out of or back into it while dragging
            ManipulationMode = TimeLineManipulationMode.Free;
            _LeftCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl);
            _RightCtrlDown = Keyboard.IsKeyDown(Key.RightCtrl);
            if (_LeftCtrlDown || _RightCtrlDown)
            {
                ManipulationMode = TimeLineManipulationMode.Linked;
            }
            #endregion
        }



        #region get children methods

        /// <summary>
        /// Returns a list of all timeline controls starting with the current one and moving forward
        /// so long as they are contiguous.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private List<TimeLineItemControl> GetTimeLineForwardChain(TimeLineItemControl current, int afterIndex, ref bool ChainsToTotalFrames, ref double chainGap)
        {
            List<TimeLineItemControl> returner = new List<TimeLineItemControl>() { current };
            double left = 0, width = 0, end = 0;
            current.GetPlacementInfo(ref left, ref width, ref end);
            DateTime endTime = current.EndTime;
            DateTime maxTime = (DateTime)new UshortDateConverter().Convert(TotalFrames, null, null, null);
            Console.WriteLine("item end: " + endTime + ", maxWidth: " + maxTime);
            if (afterIndex < 0)
            {
                //we are on the end of the list so there is no limit. -> LOL, no, now there is
                chainGap = double.MaxValue;
                ChainsToTotalFrames = endTime.CompareTo(maxTime) >= 0;
                return returner;
            }
            double bumpThreshold = _BumpThreshold;
            double lastAddedEnd = end;
            while (afterIndex < Items.Count)
            {
                left = width = end = 0;
                var checker = GetTimeLineItemControlAt(afterIndex++);
                if (checker != null)
                {
                    checker.GetPlacementInfo(ref left, ref width, ref end);
                    endTime = checker.EndTime;
                    double gap = left - lastAddedEnd;
                    if (gap > bumpThreshold)
                    {
                        chainGap = gap;
                        ChainsToTotalFrames = endTime.CompareTo(maxTime) >= 0;
                        return returner;
                    }
                    returner.Add(checker);
                    lastAddedEnd = end;
                }
            }
            //we have chained off to the end and thus have no need to worry about our gap -> We do though, the end is near
            ChainsToTotalFrames = endTime.CompareTo(maxTime) >= 0;
            chainGap = double.MaxValue;
            return returner;
        }

        /// <summary>
        /// Returns a list of all timeline controls starting with the current one and moving backwoards
        /// so long as they are contiguous.  If the chain reaches back to the start time of the timeline then the
        /// ChainsBackToStart bool is modified to reflect that.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private List<TimeLineItemControl> GetTimeLineBackwardsChain(TimeLineItemControl current, int prevIndex, ref bool ChainsBackToStart, ref double chainGap)
        {
            List<TimeLineItemControl> returner = new List<TimeLineItemControl>() { current };
            double left = 0, width = 0, end = 0;
            current.GetPlacementInfo(ref left, ref width, ref end);
            if (prevIndex < 0)
            {
                chainGap = double.MaxValue;
                ChainsBackToStart = left == 0;
                return returner;
            }

            double lastAddedLeft = left;
            while (prevIndex >= 0)
            {
                left = width = end = 0;

                var checker = GetTimeLineItemControlAt(prevIndex--);
                if (checker != null)
                {
                    checker.GetPlacementInfo(ref left, ref width, ref end);
                    if (lastAddedLeft - end > _BumpThreshold)
                    {
                        //our chain just broke;
                        chainGap = lastAddedLeft - end;
                        ChainsBackToStart = lastAddedLeft == 0;
                        return returner;
                    }
                    returner.Add(checker);
                    lastAddedLeft = left;
                }
            }
            ChainsBackToStart = lastAddedLeft == 0;
            chainGap = lastAddedLeft;//gap between us and zero;
            return returner;
        }

        private TimeLineItemControl GetTimeLineItemControlStartingBefore(DateTime dateTime, ref int index)
        {
            index = -1;
            for (int i = 0; i < Items.Count; i++)
            {
                var checker = GetTimeLineItemControlAt(i);
                if (checker != null && checker.StartTime == dateTime && i != 0)
                {
                    index = i - 1;
                    return GetTimeLineItemControlAt(i - 1);
                }
            }
            index = -1;
            return null;
        }

        private TimeLineItemControl GetTimeLineItemControlStartingAfter(DateTime dateTime, ref int index)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var checker = GetTimeLineItemControlAt(i);
                if (checker != null && checker.StartTime > dateTime)
                {
                    index = i;
                    return checker;
                }
            }
            index = -1;
            return null;
        }

        private TimeLineItemControl GetTimeLineItemControlAt(int i)
        {
            //child 0 is our background grid.
            i++;
            if (i <= 0 || i >= Children.Count)
                return null;
            return Children[i] as TimeLineItemControl;
        }

        #endregion
    }
}
