﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using Emgu.CV.External.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using Tools.FlockingDevice.Tracking.Data;
using Tools.FlockingDevice.Tracking.Model;
using Tools.FlockingDevice.Tracking.Processor;
using Tools.FlockingDevice.Tracking.Properties;
using Tools.FlockingDevice.Tracking.Sources;
using Application = System.Windows.Application;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Tools.FlockingDevice.Tracking.ViewModel
{
    public class PipelineViewModel : ViewModelBase
    {
        #region commands

        public RelayCommand<RgbProcessor> RemoveProcessorCommand { get; private set; }

        public RelayCommand<DragEventArgs> DragOverCommand { get; private set; }

        public RelayCommand<DragEventArgs> DropSourceCommand { get; private set; }

        public RelayCommand<DragEventArgs> DropTargetColorCommand { get; private set; }

        public RelayCommand<DragEventArgs> DropTargetDepthCommand { get; private set; }

        #endregion

        #region properties

        #region Model

        /// <summary>
        /// The <see cref="Model" /> property's name.
        /// </summary>
        public const string ModelPropertyName = "Model";

        private Pipeline _model;

        /// <summary>
        /// Sets and gets the Model property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public Pipeline Model
        {
            get
            {
                return _model;
            }

            set
            {
                if (_model == value)
                {
                    return;
                }

                RaisePropertyChanging(ModelPropertyName);
                _model = value;
                RaisePropertyChanged(ModelPropertyName);
            }
        }

        #endregion

        #region ColorImage

        /// <summary>
        /// The <see cref="ColorImage" /> property's name.
        /// </summary>
        public const string ColorImagePropertyName = "ColorImage";

        private BitmapSource _colorImage;

        /// <summary>
        /// Sets and gets the ColorImage property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public BitmapSource ColorImage
        {
            get
            {
                return _colorImage;
            }

            set
            {
                if (_colorImage == value)
                {
                    return;
                }

                RaisePropertyChanging(ColorImagePropertyName);
                _colorImage = value;
                RaisePropertyChanged(ColorImagePropertyName);
            }
        }

        #endregion

        #region DepthImage

        /// <summary>
        /// The <see cref="DepthImage" /> property's name.
        /// </summary>
        public const string DepthImagePropertyName = "DepthImage";

        private BitmapSource _depthImage;

        /// <summary>
        /// Sets and gets the DepthImage property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public BitmapSource DepthImage
        {
            get
            {
                return _depthImage;
            }

            set
            {
                if (_depthImage == value)
                {
                    return;
                }

                RaisePropertyChanging(DepthImagePropertyName);
                _depthImage = value;
                RaisePropertyChanged(DepthImagePropertyName);
            }
        }

        #endregion

        #endregion

        #region ctor

        public PipelineViewModel()
        {
            // exit hook to stop input source
            Application.Current.Exit += (s, e) => Stop();

            PropertyChanged += (_s, _e) =>
            {
                if (Equals(ModelPropertyName, _e.PropertyName))
                {
                    Model.PropertyChanging += (s, e) =>
                    {
                        if (Model.InputSource == null) return;

                        switch (e.PropertyName)
                        {
                            case Pipeline.InputSourcePropertyName:
                                Model.InputSource.Stop();
                                Model.InputSource.ImageReady -= OnImageReady;
                                break;
                        }
                    };

                    Model.PropertyChanged += (s, e) =>
                    {
                        if (Model.InputSource == null) return;

                        switch (e.PropertyName)
                        {
                            case Pipeline.InputSourcePropertyName:
                                Model.InputSource.ImageReady += OnImageReady;
                                break;
                        }
                    };
                }
            };

            RemoveProcessorCommand = new RelayCommand<RgbProcessor>(processor =>
            {
                if (Model == null) return;

                Model.ColorImageProcessors.Remove(processor);
                Model.DepthImageProcessors.Remove(processor);
            });

            #region Drag & Drop

            DragOverCommand = new RelayCommand<DragEventArgs>(
                e =>
                {
                    if (!e.Data.GetFormats().Any(f => Equals(typeof(RgbProcessor).Name, f)) &&
                        !e.Data.GetFormats().Any(f => Equals(typeof(IInputSource).Name, f)))
                    {
                        e.Effects = DragDropEffects.None;
                    }

                    //var currentMousePosition = e.GetPosition(_topLevelGrid);

                    //if (_topLevelGrid != null && _draggedAdorner != null)
                    //    _draggedAdorner.UpdateAdornerPosition(_topLevelGrid, currentMousePosition);
                });

            DropTargetColorCommand = new RelayCommand<DragEventArgs>(OnDropTargetColor);
            DropTargetDepthCommand = new RelayCommand<DragEventArgs>(OnDropTargetDepth);

            // if dropping on the source list, remove the adorner
            DropSourceCommand = new RelayCommand<DragEventArgs>(e =>
            {
                //RemoveAdorner(_listBoxItem, _topLevelGrid)
            });

            #endregion
        }

        private void OnDropTargetColor(DragEventArgs e)
        {
            OnDropTarget(Model.ColorImageProcessors, e);
        }

        private void OnDropTargetDepth(DragEventArgs e)
        {
            OnDropTarget(Model.DepthImageProcessors, e);
        }

        private void OnDropTarget(ICollection<RgbProcessor> toTarget, DragEventArgs e)
        {
            if (!e.Data.GetFormats().Any(f => Equals(typeof(RgbProcessor).Name, f))) return;
            var processorType = e.Data.GetData(typeof(RgbProcessor).Name) as Type;

            if (processorType == null)
                return;

            RgbProcessor processor;
            try
            {
                processor = Activator.CreateInstance(processorType) as RgbProcessor;
            }
            catch (Exception)
            {
                processor = Activator.CreateInstance(processorType, Model.InputSource) as RgbProcessor;
            }

            toTarget.Add(processor); // add to new collection
        }

        #endregion

        #region public methods

        public void Start()
        {
            if (Model != null && Model.InputSource != null)
                Model.InputSource.Start();
        }

        public void Stop()
        {
            if (Model != null && Model.InputSource != null)
                Model.InputSource.Stop();
        }

        public void Pause()
        {
            if (Model != null && Model.InputSource != null)
                Model.InputSource.Pause();
        }

        public void Resume()
        {
            if (Model != null && Model.InputSource != null)
                Model.InputSource.Resume();
        }

        public void Save()
        {
            var filename = Settings.Default.PipelineFilename;
            var tempFilename = string.Format("{0}.tmp", filename);

            try
            {
                var serializer = new XmlSerializer(typeof(Pipeline));
                using (var stream = new FileStream(tempFilename, FileMode.Create))
                {
                    var xmlTextWriter = XmlWriter.Create(stream, new XmlWriterSettings { NewLineChars = Environment.NewLine, Indent = true });
                    serializer.Serialize(xmlTextWriter, Model);
                }

                var bakFilename = string.Format("{0}.bak", Settings.Default.PipelineFilename);
                File.Replace(tempFilename, filename, bakFilename);
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format(@"Could not save pipeline.{0}Exception Message: {1}", Environment.NewLine, e.Message), @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Load()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Pipeline));
                using (var stream = new FileStream(Settings.Default.PipelineFilename, FileMode.Open))
                {
                    Model = serializer.Deserialize(stream) as Pipeline;
                }

                if (Model != null)
                    Model.InputSource.ImageReady += OnImageReady;
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("Could not load pipeline. {0}.", e.Message), @"Pipeline Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Image Source

        private void OnImageReady(object sender, ImageEventArgs e)
        {
            //Console.WriteLine("OnImageReady");

            #region Color Image Handling

            //if (_colorImageTask != null)
            //    _colorImageTask.Wait();

            //_colorImageTask = Task.Factory.StartNew(() =>
            //{
            var allColorData = new IData[]
            {
                new RgbImageData(e.ColorImage) 
            };

            var colorImageCopy = e.ColorImage.Copy();
            DispatcherHelper.RunAsync(() =>
            {
                ColorImage = colorImageCopy.ToBitmapSource();
                colorImageCopy.Dispose();
            });

            if (Model.ColorImageProcessors.Any())
                foreach (var colorProcessor in Model.ColorImageProcessors.ToArray())
                {
                    allColorData = colorProcessor.Process(allColorData);
                    //image.Dispose();
                }
            //});

            #endregion

            #region Depth Image Handling

            //if (_depthImageTask != null)
            //    _depthImageTask.Wait();

            //_depthImageTask = Task.Factory.StartNew(() =>
            //{
            var allDepthData = new IData[]
            {
                new RgbImageData(e.DepthImage) 
            };

            var depthImageCopy = e.DepthImage.Copy();
            DispatcherHelper.RunAsync(() =>
            {
                DepthImage = depthImageCopy.ToBitmapSource();
                depthImageCopy.Dispose();
            });

            if (Model.DepthImageProcessors.Any())
                foreach (var depthProcessor in Model.DepthImageProcessors.ToArray())
                {
                    allDepthData = depthProcessor.Process(allDepthData);
                    //image.Dispose();
                }
            //});

            #endregion

            Thread.Sleep(25);
        }

        #endregion
    }
}
