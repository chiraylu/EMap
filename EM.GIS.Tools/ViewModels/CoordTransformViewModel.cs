using EM.GIS.CoordinateTransformation;
using EM.GIS.GdalExtensions;
using EM.WpfBase;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
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
        private string _srcDirectory;
        /// <summary>
        /// ԭ�ļ�Ŀ¼
        /// </summary>
        public string SrcDirectory
        {
            get { return _srcDirectory; }
            set { SetProperty(ref _srcDirectory, value); }
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

        private string _destDirectory;
        /// <summary>
        /// Ŀ���ļ�Ŀ¼
        /// </summary>
        public string DestDirectory
        {
            get { return _destDirectory; }
            set { SetProperty(ref _destDirectory, value); }
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
        public static string GetSelectedDirectory()
        {
            string directory = string.Empty;
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title="��ѡ��Ŀ¼"
            };
            if (dialog.ShowDialog()== CommonFileDialogResult.Ok)
            {
                directory = dialog.FileName;
            }
            return directory;
        }
        private void SelectPath(string? pathName)
        {
            switch (pathName)
            {
                case nameof(SrcDirectory):
                    SrcDirectory=GetSelectedDirectory();
                    break;
                case nameof(DestDirectory):
                    DestDirectory=GetSelectedDirectory();
                    break;
                default:
                    return;
            }
        }

        private void CoordTransformViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SrcDirectory):
                case nameof(DestDirectory):
                    TransformCmd.RaiseCanExecuteChanged();
                    break;
            }
        }

        private bool CanTransform()
        {
            return !string.IsNullOrEmpty(SrcDirectory)&&!string.IsNullOrEmpty(DestDirectory);
        }
        /// <summary>
        /// ��ȡ����ת����������
        /// </summary>
        /// <returns>����ת����������</returns>
        private static Func<double, double, (double Lat, double Lon)> GetTransformFunc(OffsetType srcOffsetType, OffsetType destOffsetType)
        {
            Func<double, double, (double Lat, double Lon)> ret = (lat, lon) => (lat, lon);
            switch (srcOffsetType)
            {
                case OffsetType.None:
                    switch (destOffsetType)
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
                    switch (destOffsetType)
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
                    switch (destOffsetType)
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
        /// <summary>
        /// У������
        /// </summary>
        /// <param name="srcPath">Դ����</param>
        /// <param name="destPath">Ŀ������</param>
        /// <param name="srcOffsetType">ԭ��������</param>
        /// <param name="destOffsetType">Ŀ����������</param>
        public static void CorrectCoords(string srcPath, string destPath, OffsetType srcOffsetType, OffsetType destOffsetType)
        {
            var transformFunc = GetTransformFunc(srcOffsetType, destOffsetType);
            CorrectCoords(srcPath, destPath, transformFunc);
        }
        /// <summary>
        /// У������
        /// </summary>
        /// <param name="srcPath">Դ����</param>
        /// <param name="destPath">Ŀ������</param>
        /// <param name="transformFunc">У������</param>
        public static void CorrectCoords(string srcPath, string destPath, Func<double, double, (double Lat, double Lon)> transformFunc)
        {
            using var srcDataSource = Ogr.Open(srcPath, 0);
            if (srcDataSource==null)
            {
                Console.WriteLine($"�޷���ȡ{srcPath}");
                //MessageBox.Show(Window.GetWindow(View), $"�޷���ȡ{srcPath}");
                return;
            }
            using var driver = Ogr.GetDriverByName("ESRI Shapefile");
            using var destDataSource = driver.CopyDataSourceUTF8(srcDataSource, destPath, null);
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
                                break;
                            default:
                                Console.WriteLine("����ϵ����Ϊ4326");
                                break;
                        }
                    }
                }
            }
        }
        private void Transform()
        {
            if (SrcOffsetType== DestOffsetType)
            {
                MessageBox.Show(Window.GetWindow(View), "����ѡ����ͬƫ������");
                return;
            }
            if (SrcDirectory==DestDirectory)
            {
                MessageBox.Show(Window.GetWindow(View), "Ŀ������Ŀ¼������ԭʼ����Ŀ¼��ͬ");
                return;
            }
            var srcFiles = Directory.GetFiles(SrcDirectory);
            if (srcFiles.Length>0)
            {
                var transformFunc = GetTransformFunc(SrcOffsetType, DestOffsetType);
                foreach (var srcFile in srcFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(srcFile);
                    var destFile = Path.Combine(DestDirectory, $"{name}.shp");
                    CorrectCoords(srcFile, destFile, transformFunc);
                }
                MessageBox.Show(Window.GetWindow(View), "ת���ɹ�");
            }
        }
    }
}
