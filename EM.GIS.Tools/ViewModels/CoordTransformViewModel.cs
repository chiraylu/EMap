using EM.GIS.CoordinateTransformation;
using EM.GIS.GdalExtensions;
using EM.WpfBase;
using Microsoft.Win32;
using OSGeo.OGR;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace EM.GIS.Tools
{
    /// <summary>
    /// ����ת����ͼģ��
    /// </summary>
    public class CoordTransformViewModel : WpfBase.ViewModel<CoordTransformControl>
    {
        private OffsetType _srcOffsetType;
        /// <summary>
        /// ԭ����ƫ������
        /// </summary>
        public OffsetType SrcOffsetType
        {
            get { return _srcOffsetType; }
            set { SetProperty(ref _srcOffsetType, value); }
        }
        private string _srcPath;
        /// <summary>
        /// ԭ�ļ�·��
        /// </summary>
        public string SrcPath
        {
            get { return _srcPath; }
            set { SetProperty(ref _srcPath, value); }
        }
        /// <summary>
        /// ƫ�����ͼ���
        /// </summary>
        public ObservableCollection<OffsetType> OffsetTypes { get; }
        private OffsetType _destOffsetType;
        /// <summary>
        /// Ŀ������ƫ������
        /// </summary>
        public OffsetType DestOffsetType
        {
            get { return _destOffsetType; }
            set { SetProperty(ref _destOffsetType, value); }
        }

        private string _destPath;
        /// <summary>
        /// Ŀ���ļ�·��
        /// </summary>
        public string DestPath
        {
            get { return _destPath; }
            set { SetProperty(ref _destPath, value); }
        }
        /// <summary>
        /// ѡ��Ŀ¼����
        /// </summary>
        public DelegateCommand<string> SelectPathCmd { get; }
        /// <summary>
        /// ����ת������
        /// </summary>
        public DelegateCommand TransformCmd { get; }
        public CoordTransformViewModel(CoordTransformControl t) : base(t)
        {
            var offsetTypes = Enum.GetValues(typeof(OffsetType));
            OffsetTypes=new ObservableCollection<OffsetType>();
            foreach (var item in offsetTypes)
            {
                OffsetTypes.Add((OffsetType)item);
            }
            SelectPathCmd=new DelegateCommand<string>(SelectPath);
            TransformCmd =new DelegateCommand(Transform, CanTransform);
            PropertyChanged+=CoordTransformViewModel_PropertyChanged;
        }

        private void SelectPath(string? pathName)
        {
            switch (pathName)
            {
                case nameof(SrcPath):
                    OpenFileDialog openFileDialog = new OpenFileDialog()
                    {
                        Title="ѡ��Ҫת����Ҫ��",
                        Filter="*.shp|*.shp"
                    };
                    if (openFileDialog.ShowDialog()==true)
                    {
                        SrcPath=openFileDialog.FileName;
                    }
                    break;
                case nameof(DestPath):
                    SaveFileDialog saveFileDialog = new SaveFileDialog()
                    {
                        Title="ѡ��Ҫ�����λ��",
                        Filter="*.shp|*.shp"
                    };
                    if (saveFileDialog.ShowDialog()==true)
                    {
                        DestPath=saveFileDialog.FileName;
                    }
                    break;
                default:
                    return;
            }
        }

        private void CoordTransformViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SrcPath):
                case nameof(DestPath):
                    TransformCmd.RaiseCanExecuteChanged();
                    break;
            }
        }

        private bool CanTransform()
        {
            return File.Exists(SrcPath)&&!string.IsNullOrEmpty(DestPath);
        }
        /// <summary>
        /// ��ȡ����ת����������
        /// </summary>
        /// <returns>����ת����������</returns>
        private Func<double, double, (double Lat, double Lon)> GetTransformFunc()
        {
            Func<double, double, (double Lat, double Lon)> ret = (lat, lon) => (lat, lon);
            switch (SrcOffsetType)
            {
                case OffsetType.None:
                    switch (DestOffsetType)
                    {
                        case OffsetType.Gcj02:
                            ret = (lat, lon) => CoordHelper.Wgs84ToGcj02(lat, lon);
                            break;
                        case OffsetType.Bd09:
                            ret = (lat, lon) => CoordHelper.Wgs84ToBd09(lat, lon);
                            break;
                    }
                    break;
                case OffsetType.Gcj02:
                    switch (DestOffsetType)
                    {
                        case OffsetType.None:
                            ret =(lat, lon) => CoordHelper.Gcj02ToWgs84(lat, lon);
                            break;
                        case OffsetType.Bd09:
                            ret =(lat, lon) => CoordHelper.Gcj02ToBd09(lat, lon);
                            break;
                    }
                    break;
                case OffsetType.Bd09:
                    switch (DestOffsetType)
                    {
                        case OffsetType.None:
                            ret =(lat, lon) => CoordHelper.Bd09ToWgs84(lat, lon);
                            break;
                        case OffsetType.Gcj02:
                            ret =(lat, lon) => CoordHelper.Bd09ToGcj02(lat, lon);
                            break;
                    }
                    break;
            }
            return ret;
        }
        private void Transform()
        {
            if (SrcOffsetType== DestOffsetType)
            {
                MessageBox.Show(Window.GetWindow(View), "����ѡ����ͬƫ������");
                return;
            }
            if (SrcPath==DestPath)
            {
                MessageBox.Show(Window.GetWindow(View), "Ŀ������Ŀ¼������ԭʼ����Ŀ¼��ͬ");
                return;
            }
            using var srcDataSource = Ogr.Open(SrcPath, 0);
            if (srcDataSource==null)
            {
                MessageBox.Show(Window.GetWindow(View), $"�޷���ȡ{SrcPath}");
                return;
            }
            using var driver = srcDataSource.GetDriver();
            string[] options = { "ENCODING=UTF-8" };//���.cpg�ļ����Խ��д����������
            using var destDataSource = driver.CopyDataSource(srcDataSource, DestPath, options);
             var layerCount = destDataSource.GetLayerCount();
            if (layerCount>0)
            {
                var layer = destDataSource.GetLayerByIndex(0);
                var featureCount = layer.GetFeatureCount(1);
                if (featureCount>0)
                {
                    var spatialReference = layer.GetSpatialRef();
                    var authorityNameAndCode = spatialReference.GetOrUpdateAuthorityNameAndCode();
                    if (authorityNameAndCode.AuthorityName=="EPSG")
                    {
                        switch (authorityNameAndCode.AuthorityCode)
                        {
                            case "4326":
                                var transformFunc = GetTransformFunc();
                                Action<double[]> transformCoordAction = (coord) =>
                                {
                                    if (transformFunc!=null&& coord!=null&& coord.Length>1)
                                    {
                                        var latLon = transformFunc(coord[1], coord[0]);
                                        coord[1]=latLon.Lat;
                                        coord[0]=latLon.Lon;
                                    }
                                };
                                for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
                                {
                                    using var feature = layer.GetFeature(featureIndex);
                                    var geometry = feature.GetGeometryRef();
                                    if (geometry==null)
                                    {
                                        Console.WriteLine($"��{featureIndex}��������Ϊ��");
                                        continue;
                                    }
                                    var geometryCopy = geometry.Clone();
                                    var dimension = geometryCopy.GetCoordinateDimension();
                                    if (dimension<2)
                                    {
                                        Console.WriteLine($"��{featureIndex}��γ��Ϊ{dimension}");
                                        continue;
                                    }
                                    geometryCopy.TransformCoord(transformCoordAction);
                                    feature.SetGeometry(geometryCopy);
                                    layer.SetFeature(feature);
                                    destDataSource.FlushCache();
                                }
                                MessageBox.Show(Window.GetWindow(View), "ת���ɹ�");
                                break;
                            default:
                                MessageBox.Show(Window.GetWindow(View), "����ϵ����Ϊ4326");
                                break;
                        }
                    }
                }
            }
        }
    }
}
