#nullable enable
using System;
using CoreGraphics;
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
			return new UIScrollViewExt();
		}

		public override void SetVirtualView(IView view)
		{
			base.SetVirtualView(view);
			((UIScrollViewExt)PlatformView).View = view;

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
				UIScrollViewExt.InvalidateMeasure(platformView);
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

	internal class UIScrollViewExt : UIScrollView, ICrossPlatformLayoutBacking
	{
		internal const nint ContentTag = 0x845fed;

		/// <summary>
		/// Flag indicating whether the parent view hierarchy should be invalidated when this view is moved to a window.
		/// Used to ensure proper layout recalculation when the view becomes visible.
		/// </summary>
		bool _invalidateParentWhenMovedToWindow;

		/// <summary>
		/// Cached result of whether this scroll view is a descendant of another UIScrollView.
		/// Null when not yet calculated, true if nested within another scroll view, false otherwise.
		/// This affects safe area handling behavior.
		/// </summary>
		bool? _scrollViewDescendant;

		/// <summary>
		/// The height constraint used in the last measure operation.
		/// Used to determine if a re-measure is needed when constraints change.
		/// </summary>
		double _lastMeasureHeight;

		/// <summary>
		/// The width constraint used in the last measure operation.
		/// Used to determine if a re-measure is needed when constraints change.
		/// </summary>
		double _lastMeasureWidth;

		/// <summary>
		/// The height constraint used in the last arrange operation.
		/// Used to determine if a re-arrange is needed when the frame changes.
		/// </summary>
		double _lastArrangeHeight;

		/// <summary>
		/// The width constraint used in the last arrange operation.
		/// Used to determine if a re-arrange is needed when the frame changes.
		/// </summary>
		double _lastArrangeWidth;		

		/// <summary>
		/// The previous effective user interface layout direction (LTR/RTL).
		/// Used to detect when the layout direction changes and trigger appropriate content repositioning.
		/// </summary>
		UIUserInterfaceLayoutDirection? _previousEffectiveUserInterfaceLayoutDirection;

		WeakReference<ICrossPlatformLayout>? _crossPlatformLayoutReference;


		/// <summary>
		/// Weak reference to the cross-platform layout that manages the content of this scroll view.
		/// Weak reference prevents circular references and allows proper garbage collection.
		/// </summary>
		WeakReference<IView>? _reference;

		/// <summary>
		/// Gets or sets the cross-platform layout that manages the content of this scroll view.
		/// The layout is responsible for measuring and arranging the scroll view's content.
		/// </summary>
		public ICrossPlatformLayout? CrossPlatformLayout
		{
			get => _crossPlatformLayoutReference != null && _crossPlatformLayoutReference.TryGetTarget(out var v) ? v : null;
			set => _crossPlatformLayoutReference = value == null ? null : new WeakReference<ICrossPlatformLayout>(value);
		}

		/// <summary>
		/// Initializes a new instance of the MauiScrollView class.
		/// </summary>
		public UIScrollViewExt()
		{
		}

		/// <summary>
		/// Called by iOS when the adjusted content inset changes (e.g., when safe area changes).
		/// This method invalidates the safe area and triggers a layout update if needed.
		/// </summary>
		public override void AdjustedContentInsetDidChange()
		{
			base.AdjustedContentInsetDidChange();

			// It looks like when this invalidates it doesn't auto trigger a layout pass
			if (!ValidateSafeArea())
			{
				InvalidateMeasure(this);
				InvalidateAncestorsMeasures(this);
			}
		}		

		/// <summary>
		/// Overrides the default UIScrollView layout behavior to integrate with MAUI's cross-platform layout system.
		/// This method handles safe area validation, measures and arranges content, and manages RTL layout adjustments.
		/// It's called by iOS whenever the view needs to be laid out, including during scrolling operations.
		/// </summary>
		internal IView? View
		{
			get => _reference != null && _reference.TryGetTarget(out var v) ? v : null;
			set => _reference = value == null ? null : new(value);
		}

		public override void LayoutSubviews()
		{
			// If there's no cross-platform layout, fall back to default UIScrollView behavior
			if (CrossPlatformLayout is null)
			{
				base.LayoutSubviews();
				return;
			}

			// Validate and update safe area if needed, invalidating constraints cache if changes occurred
			if (!ValidateSafeArea())
			{
				InvalidateConstraintsCache();
			}

			// LayoutSubviews is invoked while scrolling, so we need to arrange the content only when it's necessary.
			// This could be done via `override ScrollViewHandler.PlatformArrange` but that wouldn't cover the case
			// when the ScrollView is attached to a non-MauiView parent (i.e. DeviceTests).
			var bounds = Bounds;
			var widthConstraint = (double)bounds.Width;
			var heightConstraint = (double)bounds.Height;
			ValidateSafeArea();
			var frameChanged = _lastArrangeWidth != widthConstraint || _lastArrangeHeight != heightConstraint;

			// If the frame changed, we need to arrange (and potentially measure) the content again
			if (frameChanged)
			{
				_lastArrangeWidth = widthConstraint;
				_lastArrangeHeight = heightConstraint;

				// Check if we need to re-measure the content with the new constraints
				if (!IsMeasureValid(widthConstraint, heightConstraint))
				{
					CrossPlatformMeasure(widthConstraint, heightConstraint);
					CacheMeasureConstraints(widthConstraint, heightConstraint);
				}

				var contentSize = CrossPlatformArrange(Bounds).ToCGSize();

				// Clamp content size based on ScrollView orientation to prevent unwanted scrolling
				if (View is IScrollView scrollView)
				{
					var frameSize = Bounds.Size;
					var orientation = scrollView.Orientation;

					// Clamp width if horizontal scrolling is disabled and content is larger than frame
					if (orientation is ScrollOrientation.Vertical or ScrollOrientation.Neither && contentSize.Width > frameSize.Width)
					{
						contentSize = new CGSize(frameSize.Width, contentSize.Height);
					}

					// Clamp height if vertical scrolling is disabled and content is larger than frame
					if (orientation is ScrollOrientation.Horizontal or ScrollOrientation.Neither && contentSize.Height > frameSize.Height)
					{
						contentSize = new CGSize(contentSize.Width, frameSize.Height);
					}
				}

				// When the content size changes, we need to adjust the scrollable area size so that the content can fit in it.
				if (ContentSize != contentSize)
				{
					ContentSize = contentSize;

					// Invalidation stops at `UIScrollViews` for performance reasons,
					// but when the content size changes, we need to invalidate the ancestors
					// in case the ScrollView is configured to grow/shrink with its content.
					InvalidateAncestorsMeasures(this);
				}
			}

			base.LayoutSubviews();
		}


		/// <summary>
		/// Validates and updates the safe area configuration. This method checks if the safe area
		/// has changed and updates the internal state accordingly.
		/// </summary>
		/// <returns>True if the safe area configuration hasn't changed in a way that affects layout, false otherwise.</returns>
		bool ValidateSafeArea() => true;


		UIEdgeInsets SystemAdjustedContentInset
		{
			get
			{
				UIEdgeInsets adjusted = AdjustedContentInset;
				UIEdgeInsets content = ContentInset;

				return new UIEdgeInsets(
					adjusted.Top - content.Top,
					adjusted.Left - content.Left,
					adjusted.Bottom - content.Bottom,
					adjusted.Right - content.Right
				);
			}
		}

		/// <summary>
		/// Arranges the cross-platform content within the specified bounds, accounting for safe area adjustments.
		/// This method applies safe area insets to the bounds before arranging the content.
		/// </summary>
		/// <param name="bounds">The bounds within which to arrange the content.</param>
		/// <returns>The size of the arranged content.</returns>
		Size CrossPlatformArrange(CGRect bounds)
		{
			bounds = new Rect(new Point(), bounds.Size.ToSize());

			Size contentSize;


			double width;
			double height;
			if (SystemAdjustedContentInset == UIEdgeInsets.Zero || ContentInsetAdjustmentBehavior == UIScrollViewContentInsetAdjustmentBehavior.Never)
			{
				contentSize = CrossPlatformLayout?.CrossPlatformArrange(bounds.ToRectangle()) ?? Size.Zero;

				width = contentSize.Width;
				height = contentSize.Height;
			}
			else
			{
				contentSize = CrossPlatformLayout?.CrossPlatformArrange(new Rect(new Point(), bounds.Size.ToSize())) ?? Size.Zero;

				width = contentSize.Width;
				height = contentSize.Height;
			}


			// When using ContentInsetAdjustmentBehavior.Automatic, UIKit dynamically decides whether to apply 
			// safe area insets to the scroll view (via AdjustedContentInset) or to push them into the child view's SafeAreaInsets.
			// This decision depends on whether the scroll view is considered "scrollable"—i.e., whether the contentSize 
			// is larger than the visible bounds (after accounting for safe areas).
			//
			// If the content size is *just* smaller than or equal to the scroll view’s bounds, UIKit may decide that
			// scrolling isn’t needed and push the safe area insets into the child instead. This can cause:
			//   - content centering to behave incorrectly (e.g., not respecting safe areas),
			//   - layout loops where the child resizes in response to changing safe area insets,
			//   - instability when transitioning between scrollable and non-scrollable states.
			//
			// This logic adds safe area padding to the contentSize *only if* the content is nearly large enough to require scrolling,
			// ensuring the scroll view remains in "scrollable mode" and keeps safe area insets at the scroll view level.
			// This avoids inset flip-flopping and keeps layout behavior stable and predictable.


			contentSize = new Size(width, height);

			// For Right-To-Left (RTL) layouts, we need to adjust the content arrangement and offset
			// to ensure the content is correctly aligned and scrolled. This involves a second layout
			// arrangement with an adjusted starting point and recalculating the content offset.
			if (_previousEffectiveUserInterfaceLayoutDirection != EffectiveUserInterfaceLayoutDirection)
			{
				// In mac platform, Scrollbar is not updated based on FlowDirection, so resetting the scroll indicators
				// It's a native limitation; to maintain platform consistency, a hack fix is applied to show the Scrollbar based on the FlowDirection.
				if (OperatingSystem.IsMacCatalyst() && _previousEffectiveUserInterfaceLayoutDirection is not null)
				{
					bool showsVertical = ShowsVerticalScrollIndicator;
					bool showsHorizontal = ShowsHorizontalScrollIndicator;

					ShowsVerticalScrollIndicator = false;
					ShowsHorizontalScrollIndicator = false;

					ShowsVerticalScrollIndicator = showsVertical;
					ShowsHorizontalScrollIndicator = showsHorizontal;
				}

				if (EffectiveUserInterfaceLayoutDirection == UIUserInterfaceLayoutDirection.RightToLeft)
				{
					var horizontalOffset = contentSize.Width - bounds.Width;

					if (SystemAdjustedContentInset == UIEdgeInsets.Zero || ContentInsetAdjustmentBehavior == UIScrollViewContentInsetAdjustmentBehavior.Never)
					{
						CrossPlatformLayout?.CrossPlatformArrange(new Rect(new Point(-horizontalOffset, 0), bounds.Size.ToSize()));
					}
					else
					{
						CrossPlatformLayout?.CrossPlatformArrange(new Rect(new Point(-horizontalOffset, 0), bounds.Size.ToSize()));
					}

					ContentOffset = new CGPoint(horizontalOffset, 0);

				}
				else if (_previousEffectiveUserInterfaceLayoutDirection is not null)
				{
					ContentOffset = new CGPoint(0, ContentOffset.Y);
				}
			}

			// When switching between LTR and RTL, we need to re-arrange and offset content exactly once
			// to avoid cumulative shifts or incorrect offsets on subsequent layouts.
			_previousEffectiveUserInterfaceLayoutDirection = EffectiveUserInterfaceLayoutDirection;

			return contentSize;
		}

		/// <summary>
		/// Measures the cross-platform content with the specified constraints, accounting for safe area adjustments.
		/// This method reduces the constraints by the safe area thickness before measuring, then adds it back
		/// to the result so the container can allocate the correct space.
		/// </summary>
		/// <param name="widthConstraint">The available width for the content.</param>
		/// <param name="heightConstraint">The available height for the content.</param>
		/// <returns>The measured size of the content, including safe area adjustments if applicable.</returns>
		public Size CrossPlatformMeasure(double widthConstraint, double heightConstraint)
		{
			var crossPlatformSize = CrossPlatformLayout?.CrossPlatformMeasure(widthConstraint, heightConstraint) ?? Size.Zero;



			return crossPlatformSize;
		}

		/// <summary>
		/// Calculates the size that fits within the given constraints. This method is called by iOS
		/// when the system needs to determine the natural size of the scroll view.
		/// </summary>
		/// <param name="size">The available size constraints.</param>
		/// <returns>The size that fits within the constraints.</returns>

		public override CGSize SizeThatFits(CGSize size)
		{
			if (CrossPlatformLayout is null)
			{
				return new CGSize();
			}

			var widthConstraint = (double)size.Width;
			var heightConstraint = (double)size.Height;

			var contentSize = CrossPlatformMeasure(widthConstraint, heightConstraint);

			CacheMeasureConstraints(widthConstraint, heightConstraint);

			return contentSize;
		}

		/// <summary>
		/// Marks that ancestor measures should be invalidated when this view is moved to a window.
		/// This is used to ensure proper layout recalculation when the view becomes visible.
		/// </summary>
		void InvalidateAncestorsMeasuresWhenMovedToWindow()
		{
			_invalidateParentWhenMovedToWindow = true;
		}

		/// <summary>
		/// Invalidates the measure of this view, causing it to be re-measured and re-laid out.
		/// This method is called when the view's content or constraints change.
		/// </summary>
		/// <param name="isPropagating">Whether this invalidation is propagating up the view hierarchy.</param>
		/// <returns>True if the invalidation should stop propagating, false otherwise.</returns>
		bool InvalidateMeasure(bool isPropagating)
		{
			ValidateSafeArea();
			SetNeedsLayout();
			InvalidateConstraintsCache();

			return !isPropagating;
		}

		/// <summary>
		/// Checks if the current measure is valid for the given constraints.
		/// This helps avoid unnecessary re-measurements when the constraints haven't changed.
		/// </summary>
		/// <param name="widthConstraint">The width constraint to check.</param>
		/// <param name="heightConstraint">The height constraint to check.</param>
		/// <returns>True if the last measure is still valid for these constraints, false otherwise.</returns>
		bool IsMeasureValid(double widthConstraint, double heightConstraint)
		{
			// Check the last constraints this View was measured with; if they're the same,
			// then the current measure info is already correct, and we don't need to repeat it
			return heightConstraint == _lastMeasureHeight && widthConstraint == _lastMeasureWidth;
		}

		/// <summary>
		/// Invalidates the cached constraint values, forcing a re-measurement and re-arrangement
		/// on the next layout pass.
		/// </summary>
		void InvalidateConstraintsCache()
		{
			_lastMeasureWidth = double.NaN;
			_lastMeasureHeight = double.NaN;
			_lastArrangeWidth = double.NaN;
			_lastArrangeHeight = double.NaN;
		}

		/// <summary>
		/// Caches the measure constraints for future validation.
		/// This helps optimize performance by avoiding unnecessary re-measurements.
		/// </summary>
		/// <param name="widthConstraint">The width constraint to cache.</param>
		/// <param name="heightConstraint">The height constraint to cache.</param>
		void CacheMeasureConstraints(double widthConstraint, double heightConstraint)
		{
			_lastMeasureWidth = widthConstraint;
			_lastMeasureHeight = heightConstraint;
		}

		/// <summary>
		/// Called when the view is moved to a window (added to or removed from the view hierarchy).
		/// This method handles cleanup and initialization tasks, including invalidating cached values
		/// and triggering ancestor measure invalidation if needed.
		/// </summary>
		public override void MovedToWindow()
		{
			base.MovedToWindow();

			// Clear cached scroll view descendant status since the view hierarchy may have changed
			_scrollViewDescendant = null;

			// If ancestor measure invalidation was requested, trigger it now
			if (_invalidateParentWhenMovedToWindow)
			{
				_invalidateParentWhenMovedToWindow = false;
				InvalidateAncestorsMeasures(this);
			}
		}

		internal static void InvalidateAncestorsMeasures(UIView child)
		{
			var childMauiPlatformLayout = child as UIScrollViewExt;

			while (true)
			{
				// We verify the presence of a Window to prevent scenarios where an invalidate might propagate up the view hierarchy  
				// to a SuperView that has already been disposed. Accessing such a disposed view would result in a crash (see #24032).  
				// This validation is only possible using `IMauiPlatformView`, as it provides a way to schedule an invalidation when the view is moved to window.  
				// For other cases, we accept the risk since avoiding it could lead to the layout not being updated properly.
				if (childMauiPlatformLayout is not null && child.Window is null)
				{
					childMauiPlatformLayout.InvalidateAncestorsMeasuresWhenMovedToWindow();
					return;
				}

				var superview = child.Superview;
				if (superview is null)
				{
					return;
				}

				// Now invalidate the parent view
				var propagate = true;
				var superviewMauiPlatformLayout = superview as UIScrollViewExt;
				if (superviewMauiPlatformLayout is not null)
				{
					propagate = superviewMauiPlatformLayout.InvalidateMeasure(isPropagating: true);
				}
				else
				{
					superview.SetNeedsLayout();
				}

				if (!propagate)
				{
					// We've been asked to stop propagation, so let's stop here
					return;
				}

				child = superview;
				childMauiPlatformLayout = superviewMauiPlatformLayout;
			}
		}

		internal static void InvalidateMeasure(UIView platformView)
		{
			var propagate = true;

			if (platformView is UIScrollViewExt mauiPlatformView)
			{
				propagate = mauiPlatformView.InvalidateMeasure(false);
			}
			else
			{
				platformView.SetNeedsLayout();
			}

			if (propagate)
			{
				InvalidateAncestorsMeasures(platformView);
			}
		}
	}
}
