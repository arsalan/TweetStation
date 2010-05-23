//
// TweetCell.cs: 
//
// This shows both how to implement a custom UITableViewCell and
// how to implement a custom MonoTouch.Dialog.Element.
//
// Author:
//   Miguel de Icaza
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.Dialog;

namespace TweetStation
{
	// 
	// TweetCell used for the timelines.   It is relatlively light, and 
	// does not do highlighting.   This might work for the iPhone, but
	// for the iPad we probably should just use TweetViews that do the
	// highlighting of url-like things
	//
	public class TweetCell : UITableViewCell, IImageUpdated {
		// Do these as static to reuse across all instances
		const int userSize = 14;
		const int textSize = 15;
		const int timeSize = 10;
		
		
		const int PicSize = 48;
		const int PicXPad = 10;
		const int PicYPad = 5;
		
		const int TextLeftStart = 2 * PicXPad + PicSize;
		
		const int TextHeightPadding = 4;
		const int TextYOffset = userSize + 4;
		const int MinHeight = PicSize + 2 * PicYPad;
		const int TimeWidth = 46;
		
		static UIFont userFont = UIFont.BoldSystemFontOfSize (userSize);
		static UIFont textFont = UIFont.SystemFontOfSize (textSize);
		static UIFont timeFont = UIFont.SystemFontOfSize (timeSize);
		
		Tweet tweet;
		UILabel userLabel, textLabel, timeLabel;
		UIImageView imageView;
		UIImageView retweetView;
		
		public TweetCell (IntPtr handle) : base (handle) {
			Console.WriteLine (Environment.StackTrace);
		}
		
		// Create the UIViews that we will use here, layout happens in LayoutSubviews
		public TweetCell (UITableViewCellStyle style, NSString ident, Tweet tweet) : base (style, ident)
		{
			this.tweet = tweet;
			SelectionStyle = UITableViewCellSelectionStyle.Blue;
			
			userLabel = new UILabel () {
				TextAlignment = UITextAlignment.Left,
				Font = userFont,
			};
			
			textLabel = new UILabel () {
				Font = textFont,
				TextAlignment = UITextAlignment.Left,
				Lines = 0,
				LineBreakMode = UILineBreakMode.WordWrap
			};
			timeLabel = new UILabel () {
				Font = timeFont,
				TextColor = UIColor.LightGray,
				TextAlignment = UITextAlignment.Right,
				BackgroundColor = UIColor.Clear
			};
			imageView = new UIImageView (new RectangleF (PicXPad, PicYPad, PicSize, PicSize));
			retweetView = new UIImageView (new RectangleF (PicXPad + 30, PicYPad + 30, 23, 23));
			UpdateCell (tweet);
			
			ContentView.Add (userLabel);
			ContentView.Add (textLabel);
			ContentView.Add (timeLabel);
			ContentView.Add (imageView);
			ContentView.Add (retweetView);
		}

		// 
		// This method is called when the cell is reused to reset
		// all of the cell values
		//
		public void UpdateCell (Tweet tweet)
		{
			this.tweet = tweet;
			
			userLabel.Text = tweet.Retweeter == null ? tweet.Screename : tweet.Screename + "→" + tweet.Retweeter;
			textLabel.Text = tweet.Text;
			timeLabel.Text = Util.FormatTime (new TimeSpan (DateTime.UtcNow.Ticks - tweet.CreatedAt));
			
			var img = ImageStore.GetLocalProfilePicture (tweet.UserId);
			
			// 
			// For fake UserIDs (returned by the search), we try looking up by screename now
			//
			if (img == null)
				img = ImageStore.GetLocalProfilePicture (tweet.Screename);
			
			
			if (img == null)
				ImageStore.QueueRequestForPicture (tweet.UserId, tweet.PicUrl, this);
			else
				tweet.PicUrl = null;
			
			imageView.Image = img == null ? ImageStore.DefaultImage : img;
			
			// If no retweet, hide our image.
			if (tweet.Retweeter == null)
				retweetView.Alpha = 0;
			else {
				retweetView.Alpha = 1;
				img = ImageStore.GetLocalProfilePicture (tweet.RetweeterId);
				if (img == null)
					ImageStore.QueueRequestForPicture (tweet.RetweeterId, tweet.RetweeterPicUrl, this);
				else
					tweet.RetweeterPicUrl = null;
				
				retweetView.Image = img == null ? ImageStore.DefaultImage : img;
			}
		}

		public static float GetCellHeight (RectangleF bounds, Tweet tweet)
		{
			bounds.Height = 999;
			
			// Keep the same as LayoutSubviews
			bounds.X = TextLeftStart;
			bounds.Width -= TextLeftStart+TextHeightPadding;
			
			using (var nss = new NSString (tweet.Text)){
				var dim = nss.StringSize (textFont, bounds.Size, UILineBreakMode.WordWrap);
				return Math.Max (dim.Height + TextYOffset + 2*TextHeightPadding, MinHeight);
			}
		}
		
		// 
		// Layouts the views, called before the cell is shown
		//
		
		public override void LayoutSubviews ()
		{
			base.LayoutSubviews ();
			var full = ContentView.Bounds;
			var tmp = full;

			tmp.Width -= TextLeftStart+TextHeightPadding+TimeWidth;
			tmp.X = TextLeftStart;
			tmp.Y = TextHeightPadding;
			tmp.Height = userSize;
			userLabel.Frame = tmp;
			
			tmp = full;
			tmp.X = TextLeftStart;
			tmp.Y = TextHeightPadding;
			tmp.Height = timeSize;
			tmp.Width -= TextLeftStart+TextHeightPadding;
			timeLabel.Frame = tmp;
			
			tmp = full;
			tmp.Y += TextYOffset;
			tmp.Height -= TextYOffset;
			tmp.X = TextLeftStart;
			tmp.Width -= TextLeftStart+TextHeightPadding;
			textLabel.Frame = tmp;
		}
		
		void IImageUpdated.UpdatedImage (long onId)
		{
			// Discard notifications that might have been queued for an old cell
			if (tweet == null || (tweet.UserId != onId && tweet.RetweeterId != onId))
				return;
			
			imageView.Alpha = 0;
			// Discard the url string once the image is loaded, we wont be using it.
			if (onId == tweet.UserId){
				imageView.Image = ImageStore.GetLocalProfilePicture (onId);
				tweet.PicUrl = null;
			} else {
				retweetView.Image = ImageStore.GetLocalProfilePicture (onId);
				tweet.RetweeterPicUrl = null;
			}

			UIView.BeginAnimations (null, IntPtr.Zero);
			UIView.SetAnimationDuration (0.5);
			
			imageView.Alpha = 1;
			UIView.CommitAnimations ();
		}
	}
	
	// 
	// A MonoTouch.Dialog.Element that renders a TweetCell
	//
	public class TweetElement : Element, IElementSizing {
		static NSString key = new NSString ("tweetelement");
		public Tweet Tweet;
		
		public TweetElement (Tweet tweet) : base (null)
		{
			Tweet = tweet;	
		}
		
		// Gets a cell on demand, reusing cells
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (key) as TweetCell;
			if (cell == null)
				cell = new TweetCell (UITableViewCellStyle.Default, key, Tweet);
			else
				cell.UpdateCell (Tweet);
			
			return cell;
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			// For partial tweets we need to load the full tweet
			if (Tweet.UserId < 0)
				Tweet.LoadFullTweet (Tweet.Id, t => {
					if (t == null)
						return;
					
					Tweet = t;
					Activate (dvc, t);
				});
			else 
				Activate (dvc, Tweet);
		}

		void Activate (DialogViewController dvc, Tweet source)
		{
			var profile = new DetailTweetViewController (source);
			dvc.ActivateController (profile);
		}

		public override bool Matches (string text)
		{
			try {
			return Tweet.Screename.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1 || 
				Tweet.Text.IndexOf (text, StringComparison.InvariantCultureIgnoreCase) != -1 || 
				Tweet.Retweeter != null ? Tweet.Retweeter.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1 : false;
			} catch {
				return false;
			}
		}
		
		#region IElementSizing implementation
		public float GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			return TweetCell.GetCellHeight (tableView.Bounds, Tweet);
		}
		#endregion
	}
}
