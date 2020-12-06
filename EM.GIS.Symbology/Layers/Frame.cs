﻿using EM.GIS.Data;
using EM.GIS.Geometries;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EM.GIS.Symbology
{
    /// <summary>
    /// 包含图层操作的框架
    /// </summary>
    public class Frame : Group, IFrame
    {
        private object _lockObject = new object();
        public CancellationTokenSource CancellationTokenSource { get; set; }
        private int _width;
        public int Width => _width;
        private int _height;
        public int Height => _height;

        private Color _backGround = Color.Transparent;
        public Color BackGround
        {
            get { return _backGround; }
            set { _backGround = value; OnBackGroundChanged(); }
        }
        private Rectangle _viewBounds;
        public Rectangle ViewBounds
        {
            get { return _viewBounds; }
            set
            {
                if (_viewBounds == value)
                {
                    return;
                }
                _viewBounds = value;
                OnViewBoundsChanged();
            }
        }
        public event EventHandler BufferChanged;
        public event EventHandler ViewBoundsChanged;
        protected virtual void OnViewBoundsChanged()
        {
            ViewBoundsChanged?.Invoke(this, new EventArgs());
        }

        protected virtual void OnBufferChanged()
        {
            BufferChanged?.Invoke(this, new EventArgs());
        }

        private Image _backBuffer;
        public Image BackBuffer
        {
            get { return _backBuffer; }
            set
            {
                if (_backBuffer == value)
                {
                    return;
                }
                lock (_lockObject)
                {
                    _backBuffer?.Dispose();
                    _backBuffer = value;
                }
                OnBackBufferChanged();
            }
        }

        private void OnBackBufferChanged()
        {
            _viewBounds = new Rectangle(0, 0, _width, _height);
            OnBufferChanged();
        }

        public virtual IExtent ViewExtents
        {
            get
            {
                return _viewExtents ?? (_viewExtents = Extent != null ? Extent.Copy() : new Extent(-180, -90, 180, 90));
            }

            set
            {
                if (value == null) return;
                IExtent ext = value.Copy();
                ResetAspectRatio(ext);
                _viewExtents = value;
                OnViewExtentsChanged(_viewExtents);
            }
        }

        public Rectangle Bounds
        {
            get => new Rectangle(0, 0, _width, _height);
            set
            { }
        }
        private int _isBusyIndex;
        public bool IsBusy
        {
            get
            {
                return _isBusyIndex > 0;
            }
            set
            {
                if (value) _isBusyIndex++;
                else _isBusyIndex--;
                if (_isBusyIndex <= 0)
                {
                    _isBusyIndex = 0;
                }
            }
        }
        public Frame(int width, int height)
        {
            _width = width;
            _height = height;
            _viewBounds = new Rectangle(0, 0, _width, _height);
            DrawingLayers = new LayerCollection();
            Items.CollectionChanged += Layers_CollectionChanged;
        }

        protected void ResetAspectRatio(IExtent newEnv)
        {
            // Aspect Ratio Handling
            if (newEnv == null) return;

            // It isn't exactly an exception, but rather just an indication not to do anything here.
            if (_height == 0 || _width == 0) return;

            double controlAspect = (double)_width / _height;
            double envelopeAspect = newEnv.Width / newEnv.Height;
            var center = newEnv.Center;

            if (controlAspect > envelopeAspect)
            {
                // The Control is proportionally wider than the envelope to display.
                // If the envelope is proportionately wider than the control, "reveal" more width without
                // changing height If the envelope is proportionately taller than the control,
                // "hide" width without changing height
                newEnv.SetCenter(center, newEnv.Height * controlAspect, newEnv.Height);
            }
            else
            {
                // The control is proportionally taller than the content is
                // If the envelope is proportionately wider than the control,
                // "hide" the extra height without changing width
                // If the envelope is proportionately taller than the control, "reveal" more height without changing width
                newEnv.SetCenter(center, newEnv.Width, newEnv.Width / controlAspect);
            }

        }

        bool firstLayerAdded;
        private void Layers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!firstLayerAdded)
            {
                if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems.Count > 0)
                {
                    firstLayerAdded = true;
                }
                if (firstLayerAdded)
                {
                    ViewExtents = Extent;
                    return;
                }
            }
            ResetBuffer();
        }

        protected void OnViewExtentChanged()
        {
            ResetBuffer();
        }

        private void OnBackGroundChanged()
        {
            ResetBuffer();
        }

        protected override void OnDraw(Graphics graphics, Rectangle rectangle, IExtent extent, bool selected = false, CancellationTokenSource cancellationTokenSource = null)
        {
            base.OnDraw(graphics, rectangle, extent, selected, cancellationTokenSource);
            var visibleDrawingFeatureLayers = new List<IFeatureLayer>();
            if (DrawingLayers != null)
            {
                foreach (var item in DrawingLayers)
                {
                    if (CancellationTokenSource?.IsCancellationRequested == true)
                    {
                        break;
                    }
                    if (item.GetVisible(extent, rectangle))
                    {
                        item.Draw(graphics, rectangle, extent, selected, cancellationTokenSource);
                        if (item is IFeatureLayer featureLayer)
                        {
                            visibleDrawingFeatureLayers.Add(featureLayer);
                        }
                    }
                }
            }

            var featureLayers = GetFeatureLayers().Where(x => x.GetVisible(extent, rectangle)).Union(visibleDrawingFeatureLayers);
            var labelLayers = featureLayers.Where(x => x.LabelLayer?.GetVisible(extent, rectangle) == true).Select(x => x.LabelLayer);
            foreach (var layer in labelLayers)
            {
                if (CancellationTokenSource?.IsCancellationRequested == true)
                {
                    break;
                }
                layer.Draw(graphics, rectangle, extent, selected, CancellationTokenSource);
            }
        }
        public Point ProjToBuffer(Coordinate location)
        {
            if (_width == 0 || _height == 0) return new Point(0, 0);
            int x = (int)((location.X - ViewExtents.MinX) * (_width / ViewExtents.Width)) + ViewBounds.X;
            int y = (int)((ViewExtents.MaxY - location.Y) * (_height / ViewExtents.Height)) + ViewBounds.Y;
            return new Point(x, y);
        }
        public Rectangle ProjToBuffer(IExtent extent)
        {
            Coordinate tl = new Coordinate(extent.MinX, extent.MaxY);
            Coordinate br = new Coordinate(extent.MaxX, extent.MinY);
            Point topLeft = ProjToBuffer(tl);
            Point bottomRight = ProjToBuffer(br);
            return new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        }

        public async Task ResetBuffer()
        {
            if (ViewExtents == null || ViewExtents.IsEmpty() || Width * Height == 0)
            {
                return;
            }
            await Task.Run(() =>
            {
                Bitmap tmpBuffer = null;
                if (Width > 0 && Height > 0)
                {
                    tmpBuffer = new Bitmap(Width, Height);
                    #region 绘制BackBuffer
                    Rectangle rectangle = Bounds;
                    using (Graphics g = Graphics.FromImage(tmpBuffer))
                    {
                        using (Brush brush = new SolidBrush(BackGround))
                        {
                            g.FillRectangle(brush, rectangle);
                        }

                        int count = 2;
                        var visibleLayers = GetLayers().Where(x => x.GetVisible(ViewExtents, rectangle));
                        for (int i = 0; i < count; i++)
                        {
                            if (CancellationTokenSource?.IsCancellationRequested == true)
                            {
                                break;
                            }
                            bool selected = i == 1;
                            Draw(g, rectangle, ViewExtents, selected, CancellationTokenSource);
                        }
                    }
                    #endregion
                }
                BackBuffer = tmpBuffer;
            });
        }
        public void Draw(Graphics g, Rectangle rectangle)
        {
            if (BackBuffer != null && g != null)
            {
                Rectangle clipView = ParentToView(rectangle);
                try
                {
                    lock (_lockObject)
                    {
                        g.DrawImage(BackBuffer, rectangle, clipView, GraphicsUnit.Pixel);
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"绘制缓存失败，{e}");
                }
            }
        }
        /// <summary>
        /// 通过将当前视图矩形与父控件的大小进行比较，获得相对于背景图像的矩形
        /// </summary>
        /// <param name="clip"></param>
        /// <returns></returns>
        public Rectangle ParentToView(Rectangle clip)
        {
            Rectangle result = new Rectangle
            {
                X = ViewBounds.X + (clip.X * ViewBounds.Width / _width),
                Y = ViewBounds.Y + (clip.Y * ViewBounds.Height / _height),
                Width = clip.Width * ViewBounds.Width / _width,
                Height = clip.Height * ViewBounds.Height / _height
            };
            return result;
        }

        public void ResetExtents()
        {
            IExtent env = BufferToProj(ViewBounds);
            ViewExtents = env;
        }
        public IExtent BufferToProj(Rectangle rect)
        {
            Point tl = new Point(rect.X, rect.Y);
            Point br = new Point(rect.Right, rect.Bottom);

            Coordinate topLeft = BufferToProj(tl);
            Coordinate bottomRight = BufferToProj(br);
            return new Extent(topLeft.X, bottomRight.Y, bottomRight.X, topLeft.Y);
        }
        public Coordinate BufferToProj(Point position)
        {
            Coordinate coordinate = null;
            if (ViewExtents != null)
            {
                double x = (position.X * ViewExtents.Width / _width) + ViewExtents.MinX;
                double y = ViewExtents.MaxY - (position.Y * ViewExtents.Height / _height);
                coordinate = new Coordinate(x, y, 0.0);
            }
            return coordinate;
        }

        public void Resize(int width, int height)
        {
            var dx = width - _width;
            var dy = height - _height;
            var destWidth = ViewBounds.Width + dx;
            var destHeight = ViewBounds.Height + dy;

            // Check for minimal size of view.
            if (destWidth < 5) destWidth = 5;
            if (destHeight < 5) destHeight = 5;

            _viewBounds = new Rectangle(ViewBounds.X, ViewBounds.Y, destWidth, destHeight);
            ResetExtents();

            _width = width;
            _height = height;
        }

        public void ZoomToMaxExtent()
        {
            ViewExtents = GetMaxExtent(true);
        }
        public IExtent GetMaxExtent(bool expand = false)
        {
            // to prevent exception when zoom to map with one layer with one point
            const double Eps = 1e-7;
            var maxExtent = Extent.Width < Eps || Extent.Height < Eps ? new Extent(Extent.MinX - Eps, Extent.MinY - Eps, Extent.MaxX + Eps, Extent.MaxY + Eps) : Extent.Copy();
            if (expand) maxExtent.ExpandBy(maxExtent.Width / 10, maxExtent.Height / 10);
            return maxExtent;
        }
        private IExtent _viewExtents;
        private int _extentChangedSuspensionCount;
        public event EventHandler UpdateMap;
        public event EventHandler<ExtentArgs> ViewExtentsChanged;

        public bool ExtentsInitialized { get; set; }

        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.AntiAlias;


        public ILayerCollection DrawingLayers { get; }

        protected virtual void OnViewExtentsChanged(IExtent ext)
        {
            if (_extentChangedSuspensionCount > 0) return;
            ResetBuffer();
            ViewExtentsChanged?.Invoke(this, new ExtentArgs(ext));
        }

    }
}
