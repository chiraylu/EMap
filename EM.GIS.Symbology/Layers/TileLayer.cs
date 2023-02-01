﻿using BruTile;
using BruTile.Web;
using EM.Bases;
using EM.GIS.Data;
using EM.GIS.Geometries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EM.GIS.Symbology
{
    /// <summary>
    /// 栅格图层
    /// </summary>
    public class TileLayer : RasterLayer, ITileLayer
    {
        private LockContainer _lockContainer = new LockContainer();
        /// <inheritdoc/>
        public new ITileSet? DataSet
        {
            get
            {
                if (base.DataSet is ITileSet dataset)
                {
                    return dataset;
                }
                else
                {
                    throw new Exception($"{nameof(DataSet)}类型必须为{nameof(ITileSet)}");
                }
            }
            set => base.DataSet = value;
        }

        public TileLayer(ITileSet rasterSet) : base(rasterSet)
        {
            Children = new RasterCategoryCollection(this);
        }

        /// <inheritdoc/>
        protected override RectangleF OnDraw(MapArgs mapArgs, bool selected = false, Action<string, int>? progressAction = null, Func<bool>? cancelFunc = null, Action<RectangleF>? invalidateMapFrameAction = null)
        {
            RectangleF ret = RectangleF.Empty;
            if (selected || cancelFunc?.Invoke() == true)
            {
                return ret;
            }
            Action<int> newProgressAction = (progress) => progressAction?.Invoke(ProgressMessage, progress);
            Draw(mapArgs, newProgressAction, cancelFunc);
            return ret;
        }
        private List<TileInfo> GetTileInfos(MapArgs mapArgs)
        {
            if (DataSet == null)
            {
                return new List<TileInfo>();
            }
            else
            {
                // 若为投影坐标系，记录投影坐标范围
                IExtent geogExtent = mapArgs.DestExtent.Copy();//地理范围
                IExtent destExtent = mapArgs.DestExtent.Copy();//要下载的地图范围
                var destRectangle = mapArgs.ProjToPixel(mapArgs.DestExtent);
                switch (DataSet.Projection.EPSG)
                {
                    case 3857:
                        destExtent.MinX = TileCalculator.Clip(destExtent.MinX, TileCalculator.MinWebMercX, TileCalculator.MaxWebMercX);
                        destExtent.MaxX = TileCalculator.Clip(destExtent.MaxX, TileCalculator.MinWebMercX, TileCalculator.MaxWebMercX);
                        destExtent.MinY = TileCalculator.Clip(destExtent.MinY, TileCalculator.MinWebMercY, TileCalculator.MaxWebMercY);
                        destExtent.MaxY = TileCalculator.Clip(destExtent.MaxY, TileCalculator.MinWebMercY, TileCalculator.MaxWebMercY);
                        destRectangle = mapArgs.ProjToPixel(destExtent);
                        geogExtent = destExtent.Copy();
                        DataSet.Projection.ReProject(4326, geogExtent);
                        break;
                    case 4326:
                        destExtent.MinX = TileCalculator.Clip(destExtent.MinX, TileCalculator.MinLongitude, TileCalculator.MaxLongitude);
                        destExtent.MinX = TileCalculator.Clip(destExtent.MaxX, TileCalculator.MinLongitude, TileCalculator.MaxLongitude);
                        destExtent.MinY = TileCalculator.Clip(destExtent.MinY, TileCalculator.MinLatitude, TileCalculator.MaxLatitude);
                        destExtent.MaxY = TileCalculator.Clip(destExtent.MaxY, TileCalculator.MinLatitude, TileCalculator.MaxLatitude);
                        destRectangle = mapArgs.ProjToPixel(destExtent);
                        geogExtent = destExtent.Copy();
                        break;
                    default:
                        throw new Exception($"不支持的坐标系 {DataSet.Projection.EPSG}");
                }
                var tileInfos = GetTileInfos(geogExtent, destExtent, destRectangle); // 计算要下载的瓦片
                return tileInfos;
            }
        }
        /// <inheritdoc/>
        public override bool IsDrawingInitialized(MapArgs mapArgs)
        {
            bool ret = false;
            if (DataSet == null)
            {
                return ret;
            }
            var tileInfos = GetTileInfos(mapArgs); // 计算要下载的瓦片
            if (tileInfos.Count == 0)
            {
                ret = true;
            }
            else
            {
                ret = true;
                foreach (var tileInfo in tileInfos)
                {
                    if (!DataSet.Tiles.Any(x => x.Key == tileInfo.Index))
                    {
                        ret = false;
                        break;
                    }
                }
            }
            return ret;
        }
        /// <inheritdoc/>
        public override void InitializeDrawing(MapArgs mapArgs, Func<bool>? cancelFunc = null)
        {
            InitializeDrawing(mapArgs, null, cancelFunc);
        }
        private void InitializeDrawing(MapArgs mapArgs, Action<int>? progressAction = null, Func<bool>? cancelFunc = null)
        {
            if (mapArgs == null || mapArgs.Graphics == null || mapArgs.Bound.IsEmpty || mapArgs.Extent == null || mapArgs.Extent.IsEmpty() || mapArgs.DestExtent == null || mapArgs.DestExtent.IsEmpty() || cancelFunc?.Invoke() == true || DataSet == null)
            {
                return;
            }
            try
            {
                progressAction?.Invoke(10);
                if (cancelFunc?.Invoke() == true) return;

                if (cancelFunc?.Invoke() == true) return;
                // 若为投影坐标系，记录投影坐标范围
                IExtent geogExtent = mapArgs.DestExtent.Copy();//地理范围
                IExtent destExtent = mapArgs.DestExtent.Copy();//要下载的地图范围
                var destRectangle = mapArgs.ProjToPixel(mapArgs.DestExtent);
                switch (DataSet.Projection.EPSG)
                {
                    case 3857:
                        destExtent.MinX = TileCalculator.Clip(destExtent.MinX, TileCalculator.MinWebMercX, TileCalculator.MaxWebMercX);
                        destExtent.MaxX = TileCalculator.Clip(destExtent.MaxX, TileCalculator.MinWebMercX, TileCalculator.MaxWebMercX);
                        destExtent.MinY = TileCalculator.Clip(destExtent.MinY, TileCalculator.MinWebMercY, TileCalculator.MaxWebMercY);
                        destExtent.MaxY = TileCalculator.Clip(destExtent.MaxY, TileCalculator.MinWebMercY, TileCalculator.MaxWebMercY);
                        destRectangle = mapArgs.ProjToPixel(destExtent);
                        geogExtent = destExtent.Copy();
                        DataSet.Projection.ReProject(4326, geogExtent);
                        break;
                    case 4326:
                        destExtent.MinX = TileCalculator.Clip(destExtent.MinX, TileCalculator.MinLongitude, TileCalculator.MaxLongitude);
                        destExtent.MinX = TileCalculator.Clip(destExtent.MaxX, TileCalculator.MinLongitude, TileCalculator.MaxLongitude);
                        destExtent.MinY = TileCalculator.Clip(destExtent.MinY, TileCalculator.MinLatitude, TileCalculator.MaxLatitude);
                        destExtent.MaxY = TileCalculator.Clip(destExtent.MaxY, TileCalculator.MinLatitude, TileCalculator.MaxLatitude);
                        destRectangle = mapArgs.ProjToPixel(destExtent);
                        geogExtent = destExtent.Copy();
                        break;
                    default:
                        throw new Exception($"不支持的坐标系 {DataSet.Projection.EPSG}");
                }

                progressAction?.Invoke(20);
                if (cancelFunc?.Invoke() == true) return;

                var tileInfos = GetTileInfos(geogExtent, destExtent, destRectangle); // 计算要下载的瓦片
                if (tileInfos.Count > 0)
                {
                    progressAction?.Invoke(30);
                    if (cancelFunc?.Invoke() == true) return;

                    int count = 0;
                    using var parallelCts = new CancellationTokenSource();
                    Func<bool> newCancelFunc = () =>
                    {
                        bool isCancel = cancelFunc?.Invoke() == true;
                        if (isCancel && !parallelCts.IsCancellationRequested)
                        {
                            parallelCts.Cancel();
                        }
                        return isCancel;
                    };

                    ParallelOptions parallelOptions = new ParallelOptions()
                    {
                        CancellationToken = parallelCts.Token
                    };
                    //var cancellationLock = _lockContainer.GetOrCreateLock("cancellationLock");
                    Parallel.ForEach(tileInfos, parallelOptions, (tileInfo) =>
                    {
                        if (cancelFunc?.Invoke() != true)
                        {
                            if (!DataSet.Tiles.ContainsKey(tileInfo.Index))// 如果未包含该瓦片，则需进行下载至缓存
                            {
                                using (var task = DataSet.GetBitmapAsync(tileInfo, 1, newCancelFunc).ContinueWith((bitmapTask) =>
                                {
                                    DataSet.AddTileToTiles(tileInfo, bitmapTask.Result, newCancelFunc);
                                }))
                                {
                                    task.ConfigureAwait(false);
                                    task.Wait(); // 等待任务完成
                                }
                            }
                            else
                            {
                                if (DataSet.Tiles.TryGetValue(tileInfo.Index, out var oldTleInfo) && oldTleInfo.IsNodata) // 重新下载nodata的瓦片
                                {
                                    using (var task = DataSet.GetBitmapAsync(tileInfo, 1, newCancelFunc).ContinueWith((bitmapTask) =>
                                    {
                                        var bitmap = bitmapTask.Result.Bitmap;
                                        if (bitmap != null)
                                        {
                                            var tileImage = new ImageSet(bitmap, oldTleInfo.Tile.Extent)
                                            {
                                                Name = DataSet.Name,
                                                Projection = DataSet.Projection,
                                                Bounds = new RasterBounds(bitmap.Height, bitmap.Width, oldTleInfo.Tile.Extent)
                                            };
                                            oldTleInfo.Tile.Dispose();
                                            DataSet.Tiles[tileInfo.Index] = (tileImage, bitmapTask.Result.IsNodata);
                                        }
                                    }))
                                    {
                                        task.ConfigureAwait(false);
                                        task.Wait(); // 等待任务完成
                                    }
                                }
                            }
                            count++;
                            progressAction?.Invoke((int)(30 + count * 60.0 / tileInfos.Count));
                        }
                    });
                }

                #region 移除级别不一致的瓦片
                var firstTileInfo = tileInfos.FirstOrDefault();
                if (firstTileInfo != null)
                {
                    for (int i = DataSet.Tiles.Count - 1; i >= 0; i--)
                    {
                        var existedTileInfo = DataSet.Tiles.ElementAt(i);
                        if (existedTileInfo.Key.Level != firstTileInfo.Index.Level)
                        {
                            if (DataSet.Tiles.TryRemove(existedTileInfo.Key, out var tileInfo))
                            {
                                tileInfo.Tile?.Dispose();
                            }
                        }
                    }
                }
                #endregion

                #region 超过缓存数后，移除多余的缓存图片
                if (DataSet.Tiles.Count > 100)
                {
                    for (int i = DataSet.Tiles.Count - 1; i >= 0; i--)
                    {
                        var existedTileInfo = DataSet.Tiles.ElementAt(i);
                        if (!tileInfos.Any(x => x.Index == existedTileInfo.Key))
                        {
                            if (DataSet.Tiles.TryRemove(existedTileInfo.Key, out var tileInfo))
                            {
                                tileInfo.Tile?.Dispose();
                            }
                        }
                    }
                }
                #endregion
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"已正常取消获取瓦片。"); // 不用管该异常
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(InitializeDrawing)}失败，{ex}");
            }
        }
        /// <summary>
        /// 根据范围获取范围内所有的瓦片信息集合
        /// </summary>
        /// <param name="geoExtent">所需下载的地理坐标系范围</param>
        /// <param name="extent">所需下载的范围</param>
        /// <param name="rectangle">窗口大小</param>
        /// <returns>瓦片信息集合</returns>
        public List<TileInfo> GetTileInfos(IExtent geoExtent, IExtent extent, RectangleF rectangle)
        {
            var ret = new List<TileInfo>();
            int minZoom = 0, maxZoom = 18;
            if (DataSet?.TileSource is HttpTileSource httpTileSource)
            {
                var levels = httpTileSource.Schema.Resolutions.Keys;
                if (levels.Count > 0)
                {
                    minZoom = levels.First();
                    maxZoom = levels.Last();
                }

                var zoom = TileCalculator.DetermineZoomLevel(geoExtent, rectangle, minZoom, maxZoom);
                ret.AddRange(httpTileSource.Schema.GetTileInfos(new BruTile.Extent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY), zoom));
            }

            return ret;
        }
        private void Draw(MapArgs mapArgs, Action<int>? progressAction = null, Func<bool>? cancelFunc = null)
        {
            if (mapArgs == null || mapArgs.Graphics == null || mapArgs.Bound.IsEmpty || mapArgs.Extent == null || mapArgs.Extent.IsEmpty() || mapArgs.DestExtent == null || mapArgs.DestExtent.IsEmpty() || cancelFunc?.Invoke() == true || DataSet == null)
            {
                return;
            }
            try
            {
                InitializeDrawing(mapArgs, progressAction, cancelFunc);
                if (cancelFunc?.Invoke() == true)
                {
                    return;
                }

                #region 绘制相交的图片
                foreach (var tile in DataSet.Tiles)
                {
                    if (cancelFunc?.Invoke() == true)
                    {
                        Debug.WriteLine($"{nameof(Draw)}取消_{tile.Key.Level}_{tile.Key.Col}_{tile.Key.Row}");
                        return;
                    }
                    if (tile.Value.Tile.Extent.Intersects(mapArgs.DestExtent))
                    {
                        tile.Value.Tile.Draw(mapArgs, progressAction, cancelFunc);
                    }
                }
                #endregion
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"已正常取消获取瓦片。"); // 不用管该异常
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(Draw)}失败，{ex}");
            }
        }
    }
}