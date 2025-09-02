namespace Syncfusion.Maui.Toolkit.Charts
{
	/// <summary>
	/// Contraint le PAN en mode horizontal uniquement.
	/// </summary>
	public class CarburoZoomPanLockHorizontalBehavior : ChartZoomPanBehavior
	{
		#region Constructeurs
		public CarburoZoomPanLockHorizontalBehavior()
		{
		}
		#endregion

		internal override void OnScrollChanged(IChart chart, Point touchPoint, Point translatePoint)
		{
			translatePoint = new Point(translatePoint.X, 0);

			base.OnScrollChanged(chart, touchPoint, translatePoint);
		}

	}
}
