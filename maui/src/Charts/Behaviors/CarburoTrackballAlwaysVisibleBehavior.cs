namespace Syncfusion.Maui.Toolkit.Charts
{
	/// <summary>
	/// Permet d'avoir le trackbar qui reste visible quand on touche le graphique.
	/// Le trackbar se cache quand on tente un glissement.
	/// </summary>	
	public partial class CarburoTrackballAlwaysVisibleBehavior : ChartTrackballBehavior
	{
		/// <summary>
		/// Permet d'avoir le trackbar qui reste visible quand on touche le graphique.
		/// Le trackbar se cache quand on tente un glissement.
		/// </summary>
		public CarburoTrackballAlwaysVisibleBehavior()
			: base()
		{
			ActivationMode = ChartTrackballActivationMode.None;

			AutoHide = false;

			SetTrackballViewInputTransparent();
		}

		private void SetTrackballViewInputTransparent()
		{
			if (CartesianChart?._trackballView != null)
			{
				CartesianChart._trackballView.InputTransparent = true;
			}
		}

		/// <summary>
		/// Handles the touch-down event on the chart and displays the associated UI element at the specified coordinates.
		/// </summary>
		/// <remarks>If the associated UI element is not currently visible, this method will display it at the
		/// specified coordinates.</remarks>
		/// <param name="chart">The chart instance where the touch-down event occurred.</param>
		/// <param name="pointX">The X-coordinate of the touch point, in chart-relative units.</param>
		/// <param name="pointY">The Y-coordinate of the touch point, in chart-relative units.</param>
		internal protected override void OnTouchDown(ChartBase chart, float pointX, float pointY)
		{
			SetTrackballViewInputTransparent();

			Show(pointX, pointY);
		}

		/// <summary>
		/// Handles the touch move event on the chart.
		/// </summary>
		/// <remarks>This method is triggered when a touch move gesture is detected on the chart.  If the associated
		/// UI element is currently shown, it will be hidden in response to the gesture.</remarks>
		/// <param name="chart">The chart instance where the touch move event occurred.</param>
		/// <param name="pointX">The X-coordinate of the touch point.</param>
		/// <param name="pointY">The Y-coordinate of the touch point.</param>
		internal protected override void OnTouchMove(ChartBase chart, float pointX, float pointY)
		{
		}

		/// <summary>
		/// Handles the touch-up event on the chart at the specified coordinates.
		/// </summary>
		/// <param name="chart">The chart instance where the touch-up event occurred.</param>
		/// <param name="x">The x-coordinate of the touch-up event, in chart-relative units.</param>
		/// <param name="y">The y-coordinate of the touch-up event, in chart-relative units.</param>
		protected internal override void OnTouchUp(ChartBase chart, float x, float y)
		{
			// On ne cache pas (AutoHide = false).
		}
	}
}
