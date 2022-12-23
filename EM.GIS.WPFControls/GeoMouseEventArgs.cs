﻿using EM.GIS.Controls;
using EM.GIS.Data;
using EM.GIS.Geometries;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Input;

namespace EM.GIS.WPFControls
{
    public class GeoMouseEventArgs : MouseEventArgs, IGeoMouseEventArgs
    {
        #region  Constructors

        public GeoMouseEventArgs(MouseEventArgs e, Map map) : base(e.MouseDevice, e.Timestamp)
        {
            if (map == null) return;

            var position = e.GetPosition(map);
            Location = new Point((int)position.X, (int)position.Y);
            GeographicLocation = map.PixelToProj(Location);
            Map = map;
        }

        #endregion

        #region Properties
        public ICoordinate GeographicLocation { get; }

        public IMap Map { get; }

        public Point Location { get; }

        #endregion
    }
}
