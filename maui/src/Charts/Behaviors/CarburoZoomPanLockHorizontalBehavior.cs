namespace Syncfusion.Maui.Toolkit.Charts
{
	/// <summary>
	/// Contraint le PAN en mode horizontal uniquement.
	/// </summary>
	public class CarburoZoomPanLockHorizontalBehavior : ChartZoomPanBehavior
	{
		#region Constructeurs
		/// <summary>
		/// Contraint le PAN en mode horizontal uniquement.
		/// </summary>
		public CarburoZoomPanLockHorizontalBehavior()
		{
		}
		#endregion

		internal override void OnScrollChanged(IChart chart, Point touchPoint, Point translatePoint)
		{
			// Ignore toute dérive verticale.
			translatePoint = new Point(translatePoint.X, 0);

			base.OnScrollChanged(chart, touchPoint, translatePoint);
		}

		internal override bool TouchHandled(SfCartesianChart cartesian, Point velocity)
		{
			// Ignore toute dérive verticale.

			return base.TouchHandled(cartesian, new Point(velocity.X, 0));
		}
	}
}
