using System;
using System.Linq;
using System.Collections.ObjectModel;

namespace Demo.WPF
{
	public class InkDocumentPointViewModel
	{
		private readonly Wacom.Devices.IInkDocumentPoint _point;

		public InkDocumentPointViewModel(Wacom.Devices.IInkDocumentPoint point)
		{
			_point = point;
		}

		public string Text => _point.Valid ? $"X:{_point.Point.X} Y:{_point.Point.Y} P:{_point.Pressure}" : "--";

		public ObservableCollection<InkDocumentStrokeViewModel> Children => null;
	}

	public class InkDocumentStrokeViewModel
	{
		private readonly Wacom.Devices.IInkDocumentStroke _stroke;
		private readonly ObservableCollection<InkDocumentPointViewModel> _points;

		public InkDocumentStrokeViewModel(Wacom.Devices.IInkDocumentStroke stroke)
		{
			_stroke = stroke;
			_points = new ObservableCollection<InkDocumentPointViewModel>(from point in stroke.Points select new InkDocumentPointViewModel(point));
		}

		public ObservableCollection<InkDocumentPointViewModel> Children => _points;
		public string Text => $"{_stroke.Timestamp} PenType={_stroke.PenType}" + (_stroke.PenId.HasValue ? $" PenId={_stroke.PenId}" : "");
	}

	public class InkDocumentLayerViewModel
	{
		private readonly Wacom.Devices.IInkDocumentLayer _layer;
		private readonly ObservableCollection<InkDocumentStrokeViewModel> _strokes;

		public InkDocumentLayerViewModel(Wacom.Devices.IInkDocumentLayer layer)
		{
			_layer = layer;
			_strokes = new ObservableCollection<InkDocumentStrokeViewModel>(from stroke in layer.Strokes select new InkDocumentStrokeViewModel(stroke));
		}

		public ObservableCollection<InkDocumentStrokeViewModel> Children => _strokes;
		public string Text => $"Layer ({_layer.Strokes.Count} stokes)";
	}

	public class InkDocumentViewModel
	{
		private readonly Wacom.Devices.IInkDocument _inkDocument;
		private readonly ObservableCollection<InkDocumentLayerViewModel> _layers;

		public InkDocumentViewModel(Wacom.Devices.IInkDocument inkDocument)
		{
			_inkDocument = inkDocument;
			if (inkDocument != null)
				_layers = new ObservableCollection<InkDocumentLayerViewModel>(from layer in inkDocument.Layers select new InkDocumentLayerViewModel(layer));
			else
				_layers = null;
		}

		public ObservableCollection<InkDocumentLayerViewModel> Children => _layers;
		public string Text => _inkDocument.CreationDate.ToString();
	}

}
