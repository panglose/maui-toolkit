#nullable enable
using System;
using System.Reflection;
using CoreGraphics;
using log4net;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Primitives;
using Syncfusion.Maui.Toolkit.Picker;
using UIKit;

namespace Syncfusion.Maui.Toolkit.Internals
{
	/// <summary>
	/// Handler iOS pour SfPickerView.
	/// Clone la logique de ScrollViewHandler (MAUI) en remplaçant ScrollEventProxy (events)
	/// par un delegate natif PickerScrollViewDelegate (WillEndDragging + Scrolled + ScrollFinished).
	/// </summary>
	internal class SfPickerScrollViewHandler :
		ViewHandler<IScrollView, UIScrollView>,
		IScrollViewHandler,
		ICrossPlatformLayout
	{
		private static ILog _Log = LogManager.GetLogger(typeof(SfPickerScrollViewHandler));		

		const nint ContentTag = 0x845fed;

		PickerScrollViewDelegate? _pickerDelegate;

		internal ScrollToRequest? PendingScrollToRequest { get; private set; }

		//
		// ─────────────────────────────────────────────────────────────────────────────
		//   Mapper & CommandMapper repris de ScrollViewHandler
		// ─────────────────────────────────────────────────────────────────────────────
		//

		public static IPropertyMapper<IScrollView, IScrollViewHandler> Mapper =
			new PropertyMapper<IScrollView, IScrollViewHandler>(ViewMapper)
			{
				[nameof(IScrollView.Content)] = MapContent,
				[nameof(IScrollView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
				[nameof(IScrollView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
				[nameof(IScrollView.Orientation)] = MapOrientation,
				[nameof(IScrollView.IsEnabled)] = MapIsEnabled,
			};

		public static CommandMapper<IScrollView, IScrollViewHandler> CommandMapper =
			new(ViewCommandMapper)
			{
				[nameof(IScrollView.RequestScrollTo)] = MapRequestScrollTo
			};

		public SfPickerScrollViewHandler() : base(Mapper, CommandMapper)
		{
		}

		public SfPickerScrollViewHandler(IPropertyMapper? mapper)
			: base(mapper ?? Mapper, CommandMapper)
		{
		}

		public SfPickerScrollViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
			: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
		{
		}

		IScrollView IScrollViewHandler.VirtualView => VirtualView;
		UIScrollView IScrollViewHandler.PlatformView => PlatformView!;

		//
		// ─────────────────────────────────────────────────────────────────────────────
		//   Création de la vue native
		// ─────────────────────────────────────────────────────────────────────────────
		//

		protected override UIScrollView CreatePlatformView()
		{
			return new MauiScrollView();
		}

		//
		// ─────────────────────────────────────────────────────────────────────────────
		//   Connect / Disconnect
		// ─────────────────────────────────────────────────────────────────────────────
		//

		protected override void ConnectHandler(UIScrollView platformView)
		{
			base.ConnectHandler(platformView);

			if (platformView is ICrossPlatformLayoutBacking platformScrollView)
			{
				platformScrollView.CrossPlatformLayout = this;
			}

			// Installation DU SEUL delegate natif (aucun event C#)
			var pickerView = VirtualView as SfPickerView;
			_pickerDelegate = new PickerScrollViewDelegate(VirtualView as IScrollView, pickerView);
			platformView.Delegate = _pickerDelegate;
		}

		protected override void DisconnectHandler(UIScrollView platformView)
		{
			if (platformView is ICrossPlatformLayoutBacking platformScrollView)
			{
				platformScrollView.CrossPlatformLayout = null;
			}

			if (platformView.Delegate == _pickerDelegate)
			{
				platformView.Delegate = null;
			}

			_pickerDelegate = null;

			base.DisconnectHandler(platformView);

			PendingScrollToRequest = null;
		}

		//
		// ─────────────────────────────────────────────────────────────────────────────
		//   MapXXX repris de ScrollViewHandler
		// ─────────────────────────────────────────────────────────────────────────────
		//

		public static void MapContent(IScrollViewHandler handler, IScrollView scrollView)
		{
			if (handler.PlatformView == null || handler.MauiContext == null)
				return;

			UpdateContentView(scrollView, handler);
		}

		// On le garde même si pas mappé, pour compatibilité
		public static void MapContentSize(IScrollViewHandler handler, IScrollView scrollView)
		{
			handler.PlatformView?.UpdateContentSize(scrollView.ContentSize);
		}

		public static void MapIsEnabled(IScrollViewHandler handler, IScrollView scrollView)
		{
			handler.PlatformView?.UpdateIsEnabled(scrollView);
		}

		public static void MapHorizontalScrollBarVisibility(IScrollViewHandler handler, IScrollView scrollView)
		{
			handler.PlatformView?.UpdateHorizontalScrollBarVisibility(scrollView.HorizontalScrollBarVisibility);
		}

		public static void MapVerticalScrollBarVisibility(IScrollViewHandler handler, IScrollView scrollView)
		{
			handler.PlatformView?.UpdateVerticalScrollBarVisibility(scrollView.VerticalScrollBarVisibility);
		}

		public static void MapOrientation(IScrollViewHandler handler, IScrollView scrollView)
		{
			if (handler?.PlatformView is not { } platformView)
			{
				return;
			}

			platformView.UpdateIsEnabled(scrollView);
			platformView.InvalidateMeasure(scrollView);
		}

		public static void MapRequestScrollTo(IScrollViewHandler handler, IScrollView scrollView, object? args)
		{
			if (args is ScrollToRequest request)
			{
				var uiScrollView = handler.PlatformView;

				if (uiScrollView == null)
					return;

				if (uiScrollView.ContentSize == CGSize.Empty && handler is SfPickerScrollViewHandler scrollViewHandler)
				{
					// Copié de ScrollViewHandler : on garde le ScrollTo en attente
					scrollViewHandler.PendingScrollToRequest = request;
					return;
				}

				var availableScrollHeight = uiScrollView.ContentSize.Height - uiScrollView.Frame.Height;
				var availableScrollWidth = uiScrollView.ContentSize.Width - uiScrollView.Frame.Width;
				var minScrollHorizontal = Math.Min(request.HorizontalOffset, availableScrollWidth);
				var minScrollVertical = Math.Min(request.VerticalOffset, availableScrollHeight);
				uiScrollView.SetContentOffset(new CGPoint(minScrollHorizontal, minScrollVertical), !request.Instant);

				if (request.Instant)
				{
					scrollView.ScrollFinished();
				}
			}
		}

		//
		// ─────────────────────────────────────────────────────────────────────────────
		//   ContentView & Layout (fidèle à ScrollViewHandler)
		// ─────────────────────────────────────────────────────────────────────────────
		//

		static void UpdateContentView(IScrollView scrollView, IScrollViewHandler handler)
		{
			bool changed = false;

			var platformView = handler.PlatformView ?? throw new InvalidOperationException($"{nameof(handler.PlatformView)} should have been set by base class.");
			var mauiContext = handler.MauiContext ?? throw new InvalidOperationException($"{nameof(handler.MauiContext)} should have been set by base class.");

			if (GetContentView(platformView) is { } currentContentPlatformView)
			{
				currentContentPlatformView.RemoveFromSuperview();
				changed = true;
			}

			if (scrollView.PresentedContent is { } content)
			{
				var platformContent = content.ToPlatform(mauiContext);
				platformContent.Tag = ContentTag;
				platformView.AddSubview(platformContent);
				changed = true;
			}

			if (changed)
			{
				MauiPrivateInvoke.InvalidateMeasure(platformView);
			}
		}
		

		static UIView? GetContentView(UIScrollView scrollView)
		{
			foreach (var subview in scrollView.Subviews)
			{
				if (subview.Tag == ContentTag)
					return subview;
			}

			return null;
		}		

		//
		// ─────────────────────────────────────────────────────────────────────────────
		//   ICrossPlatformLayout (copié de ScrollViewHandler)
		// ─────────────────────────────────────────────────────────────────────────────
		//

		Size ICrossPlatformLayout.CrossPlatformMeasure(double widthConstraint, double heightConstraint)
		{
			if (VirtualView is not { } scrollView)
			{
				return Size.Zero;
			}

			var scrollOrientation = scrollView.Orientation;
			var contentWidthConstraint = scrollOrientation is ScrollOrientation.Horizontal or ScrollOrientation.Both
				? double.PositiveInfinity
				: widthConstraint;
			var contentHeightConstraint = scrollOrientation is ScrollOrientation.Vertical or ScrollOrientation.Both
				? double.PositiveInfinity
				: heightConstraint;
			var contentSize = MeasureContent(scrollView, scrollView.Padding, contentWidthConstraint, contentHeightConstraint);

			var width = contentSize.Width <= widthConstraint ? contentSize.Width : widthConstraint;
			var height = contentSize.Height <= heightConstraint ? contentSize.Height : heightConstraint;

			width = ResolveConstraints(width, scrollView.Width, scrollView.MinimumWidth, scrollView.MaximumWidth);
			height = ResolveConstraints(height, scrollView.Height, scrollView.MinimumHeight, scrollView.MaximumHeight);

			return new Size(width, height);
		}

		internal static double ResolveConstraints(double measured, double exact, double min, double max)
		{
			var resolved = measured;

			min = Dimension.ResolveMinimum(min);

			if (Dimension.IsExplicitSet(exact))
			{
				// If an exact value has been specified, try to use that
				resolved = exact;
			}

			if (resolved > max)
			{
				// Apply the max value constraint (if any)
				// If the exact value is in conflict with the max value, the max value should win
				resolved = max;
			}

			if (resolved < min)
			{
				// Apply the min value constraint (if any)
				// If the exact or max value is in conflict with the min value, the min value should win
				resolved = min;
			}

			return resolved;
		}

		static Size MeasureContent(IContentView contentView, Thickness inset, double widthConstraint, double heightConstraint)
		{
			var content = contentView.PresentedContent;

			var contentSize = Size.Zero;

			if (!double.IsInfinity(widthConstraint) && Dimension.IsExplicitSet(contentView.Width))
			{
				widthConstraint = contentView.Width;
			}

			if (!double.IsInfinity(heightConstraint) && Dimension.IsExplicitSet(contentView.Height))
			{
				heightConstraint = contentView.Height;
			}

			if (content is not null)
			{
				contentSize = content.Measure(
					widthConstraint - inset.HorizontalThickness,
					heightConstraint - inset.VerticalThickness);
			}

			return new Size(
				contentSize.Width + inset.HorizontalThickness,
				contentSize.Height + inset.VerticalThickness);
		}

		Size ICrossPlatformLayout.CrossPlatformArrange(Rect bounds)
		{
			return (VirtualView as ICrossPlatformLayout)?.CrossPlatformArrange(bounds) ?? Size.Zero;
		}
	}

	//
	// ─────────────────────────────────────────────────────────────────────────────
	//   Delegate natif : WillEndDragging + Scrolled + ScrollFinished
	// ─────────────────────────────────────────────────────────────────────────────
	//

	internal class PickerScrollViewDelegate : UIScrollViewDelegate
	{
		readonly WeakReference<IScrollView>? _scrollViewRef;
		readonly WeakReference<SfPickerView>? _pickerViewRef;

		public PickerScrollViewDelegate(IScrollView? scrollView, SfPickerView? pickerView)
		{
			if (scrollView != null)
				_scrollViewRef = new WeakReference<IScrollView>(scrollView);

			if (pickerView != null)
				_pickerViewRef = new WeakReference<SfPickerView>(pickerView);
		}

		IScrollView? ScrollView =>
			_scrollViewRef is not null && _scrollViewRef.TryGetTarget(out var v) ? v : null;

		SfPickerView? PickerView =>
			_pickerViewRef is not null && _pickerViewRef.TryGetTarget(out var p) ? p : null;

		/// <summary>
		/// Remplace l’ancien event WillEndDragging + proxy Syncfusion.
		/// </summary>
		public override void WillEndDragging(
			UIScrollView scrollView,
			CGPoint velocity,
			ref CGPoint targetContentOffset)
		{
			var picker = PickerView;

			picker?.OnPickerViewScrollEnd((int)targetContentOffset.Y);
			

		}

		/// <summary>
		/// Remplace ScrollEventProxy.Scrolled : met à jour HorizontalOffset / VerticalOffset.
		/// </summary>
		public override void Scrolled(UIScrollView scrollView)
		{
			var sv = ScrollView;
			if (sv == null)
				return;

			sv.HorizontalOffset = scrollView.ContentOffset.X;
			sv.VerticalOffset = scrollView.ContentOffset.Y;
		}

		public override void ScrollAnimationEnded(UIScrollView scrollView)
		{
			ScrollView?.ScrollFinished();
		}		
	}

	internal static class MauiPrivateInvoke
	{
		private static readonly ILog _Log = LogManager.GetLogger(typeof(MauiPrivateInvoke));
		private static readonly MethodInfo? _invalidateMeasureMethod;

		static MauiPrivateInvoke()
		{
			//_Log.Info("MauiPrivateInvoke static ctor");

			// On récupère l’assembly MAUI lui-même
			var mauiAsm = typeof(Microsoft.Maui.IView).Assembly;

			// Et on récupère le type interne ViewExtensions
			var extType = mauiAsm.GetType("Microsoft.Maui.Platform.ViewExtensions");

			if (extType != null)
			{
				_invalidateMeasureMethod = extType.GetMethod(
					"InvalidateMeasure",
					BindingFlags.Static | BindingFlags.NonPublic,
					binder: null,
					types: new[] { typeof(UIView) },
					modifiers: null
				);
			}

			//_Log.Info($"MauiPrivateInvoke: ViewExtensions type found = {extType != null}");
			//_Log.Info($"MauiPrivateInvoke: InvalidateMeasure method found = {_invalidateMeasureMethod != null}");
		}

		public static void InvalidateMeasure(UIView view)
		{
			//_Log.Info("MauiPrivateInvoke.InvalidateMeasure called");

			if (view == null)
				return;

			if (_invalidateMeasureMethod != null)
			{
				//_Log.Info("Invoking ViewExtensions.InvalidateMeasure via reflection…");
				_invalidateMeasureMethod.Invoke(null, new object[] { view });
			}
			else
			{
				//_Log.Info("Reflection failed — fallback to SetNeedsLayout()");
				view.SetNeedsLayout();
			}
		}
	}

}
