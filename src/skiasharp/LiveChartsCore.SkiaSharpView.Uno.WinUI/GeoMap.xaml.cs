﻿// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using LiveChartsCore.SkiaSharpView.WinUI.Helpers;
using LiveChartsCore.Painting;

namespace LiveChartsCore.SkiaSharpView.WinUI;

/// <summary>
/// Defines a geographic map.
/// </summary>
public sealed partial class GeoMap : UserControl, IGeoMapView
{
    private readonly CollectionDeepObserver<IGeoSeries> _seriesObserver;
    private readonly GeoMapChart _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoMap"/> class.
    /// </summary>
    public GeoMap()
    {
        InitializeComponent();
        LiveCharts.Configure(config => config.UseDefaults());
        _core = new GeoMapChart(this);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnWheelChanged;
        PointerExited += OnPointerExited;

        SizeChanged += GeoMap_SizeChanged;

        _seriesObserver = new CollectionDeepObserver<IGeoSeries>(
            (object? sender, NotifyCollectionChangedEventArgs e) => _core?.Update(),
            (object? sender, PropertyChangedEventArgs e) => _core.Update(),
            true);

        SetValue(SeriesProperty, Enumerable.Empty<IGeoSeries>());
        SetValue(ActiveMapProperty, Maps.GetWorldMap());
        SetValue(SyncContextProperty, new object());

        Unloaded += GeoMap_Unloaded;
    }

    #region dependency props

    /// <summary>
    /// The active map property
    /// </summary>
    public static readonly DependencyProperty ActiveMapProperty =
        DependencyProperty.Register(nameof(ActiveMap), typeof(DrawnMap), typeof(GeoMap),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    /// <summary>
    /// The sync context property.
    /// </summary>
    public static readonly DependencyProperty SyncContextProperty =
       DependencyProperty.Register(
           nameof(SyncContext), typeof(object), typeof(GeoMap), new PropertyMetadata(null, OnDependencyPropertyChanged));

    /// <summary>
    /// The view command property.
    /// </summary>
    public static readonly DependencyProperty ViewCommandProperty =
       DependencyProperty.Register(
           nameof(ViewCommand), typeof(object), typeof(GeoMap), new PropertyMetadata(null,
               (DependencyObject o, DependencyPropertyChangedEventArgs args) =>
               {
                   var chart = (GeoMap)o;
                   chart._core.ViewTo(args.NewValue);
               }));

    /// <summary>
    /// The map projection property
    /// </summary>
    public static readonly DependencyProperty MapProjectionProperty =
        DependencyProperty.Register(nameof(MapProjection), typeof(MapProjection), typeof(GeoMap),
            new PropertyMetadata(MapProjection.Default, OnDependencyPropertyChanged));

    /// <summary>
    /// The series property
    /// </summary>
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(nameof(Series), typeof(IEnumerable<IGeoSeries>),
            typeof(GeoMap), new PropertyMetadata(null, (DependencyObject o, DependencyPropertyChangedEventArgs args) =>
            {
                var chart = (GeoMap)o;
                var seriesObserver = chart._seriesObserver;
                seriesObserver?.Dispose((IEnumerable<IGeoSeries>)args.OldValue);
                seriesObserver?.Initialize((IEnumerable<IGeoSeries>)args.NewValue);
                chart._core.Update();
            }));

    /// <summary>
    /// The stroke property
    /// </summary>
    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke), typeof(Paint), typeof(GeoMap),
            new PropertyMetadata(new SolidColorPaint(new SKColor(255, 255, 255, 255)) { PaintStyle = PaintStyle.Stroke }, OnDependencyPropertyChanged));

    /// <summary>
    /// The fill property
    /// </summary>
    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill), typeof(Paint), typeof(GeoMap),
            new PropertyMetadata(new SolidColorPaint(new SKColor(240, 240, 240, 255)) { PaintStyle = PaintStyle.Fill }, OnDependencyPropertyChanged));

    #endregion

    #region properties

    /// <inheritdoc cref="IGeoMapView.AutoUpdateEnabled" />
    public bool AutoUpdateEnabled { get; set; } = true;

    /// <inheritdoc cref="IGeoMapView.DesignerMode" />
    bool IGeoMapView.DesignerMode => Windows.ApplicationModel.DesignMode.DesignModeEnabled;

    /// <inheritdoc cref="IGeoMapView.SyncContext" />
    public object SyncContext
    {
        get => GetValue(SyncContextProperty);
        set => SetValue(SyncContextProperty, value);
    }

    /// <inheritdoc cref="IGeoMapView.ViewCommand" />
    public object? ViewCommand
    {
        get => GetValue(ViewCommandProperty);
        set => SetValue(ViewCommandProperty, value);
    }

    /// <inheritdoc cref="IGeoMapView.Canvas"/>
    public CoreMotionCanvas Canvas => canvas.CanvasCore;

    /// <inheritdoc cref="IGeoMapView.ActiveMap"/>
    public DrawnMap ActiveMap
    {
        get => (DrawnMap)GetValue(ActiveMapProperty);
        set => SetValue(ActiveMapProperty, value);
    }

    /// <inheritdoc cref="IGeoMapView.Width"/>
    float IGeoMapView.Width => (float)ActualWidth;

    /// <inheritdoc cref="IGeoMapView.Height"/>
    float IGeoMapView.Height => (float)ActualHeight;

    /// <inheritdoc cref="IGeoMapView.MapProjection"/>
    public MapProjection MapProjection
    {
        get => (MapProjection)GetValue(MapProjectionProperty);
        set => SetValue(MapProjectionProperty, value);
    }

    /// <inheritdoc cref="IGeoMapView.Stroke"/>
    public Paint? Stroke
    {
        get => (Paint)GetValue(StrokeProperty);
        set
        {
            if (value is not null) value.PaintStyle = PaintStyle.Stroke;
            SetValue(StrokeProperty, value);
        }
    }

    /// <inheritdoc cref="IGeoMapView.Fill"/>
    public Paint? Fill
    {
        get => (Paint)GetValue(FillProperty);
        set
        {
            if (value is not null) value.PaintStyle = PaintStyle.Fill;
            SetValue(FillProperty, value);
        }
    }

    /// <inheritdoc cref="IGeoMapView.Series"/>
    public IEnumerable<IGeoSeries> Series
    {
        get => (IEnumerable<IGeoSeries>)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    #endregion

    void IGeoMapView.InvokeOnUIThread(Action action) =>
        UnoPlatformHelpers.InvokeOnUIThread(action, DispatcherQueue);

    private void GeoMap_SizeChanged(object sender, SizeChangedEventArgs e) =>
        _core.Update();

    private void GeoMap_Unloaded(object sender, RoutedEventArgs e) =>
        _core.Unload();

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _ = CapturePointer(e.Pointer);
        var p = e.GetCurrentPoint(this);
        _core?.InvokePointerDown(new LvcPoint((float)p.Position.X, (float)p.Position.Y));
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this);
        _core?.InvokePointerMove(new LvcPoint((float)p.Position.X, (float)p.Position.Y));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this);
        _core?.InvokePointerUp(new LvcPoint((float)p.Position.X, (float)p.Position.Y));
        ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => _core?.InvokePointerLeft();

    private void OnWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_core == null) throw new Exception("core not found");
        var p = e.GetCurrentPoint(this);

        _core.ViewTo(
            new ZoomOnPointerView(
                new LvcPoint((float)p.Position.X, (float)p.Position.Y),
                p.Properties.MouseWheelDelta > 0 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut));
    }

    private static void OnDependencyPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
    {
        var chart = (GeoMap)o;
        chart._core.Update();
    }
}
