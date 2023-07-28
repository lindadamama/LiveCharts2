﻿using System.Collections.Generic;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModelsSamples.Pies.NightingaleRose;

public partial class ViewModel : ObservableObject
{
    public ViewModel()
    {
        var outer = 1d;
        var data = new[] { 6, 5, 4, 3, 2 };

        // you can convert any array, list or IEnumerable<T> to a pie series collection:
        Series = data.AsPieSeries((value, series) =>
        {
            // this method is called once per element in the array
            // we are decremting the outer radius 10 percent (0.1)
            // on every element in the array.

            series.InnerRadius = 50;
            series.MaxOuterRadius = outer;
            outer -= 0.1;
        });
    }

    public IEnumerable<ISeries> Series { get; set; }
}
