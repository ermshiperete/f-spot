//
// ProgressDialog.cs
//
// Author:
//   Stephane Delcroix <sdelcroix@src.gnome.org>
//   Ruben Vermeersch <ruben@savanne.be>
//
// Copyright (C) 2008-2010 Novell, Inc.
// Copyright (C) 2008 Stephane Delcroix
// Copyright (C) 2010 Ruben Vermeersch
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

using Gtk;

using Mono.Unix;

namespace FSpot.UI.Dialog {
	public class ProgressDialog : Gtk.Dialog {

		private bool cancelled;

		private void HandleResponse (object me, ResponseArgs args)
		{
			cancelled = true;
		}

		public enum CancelButtonType {
			Cancel,
			Stop,
			None
		};

		private int total_count;

		public ProgressBar Bar { get; private set; }

		public Label Message { get; private set; }

		public Button Button { get; private set; }

		public ProgressDialog (string title, CancelButtonType cancel_button_type, int total_count, Gtk.Window parent_window)
		{
			Title = title;
			this.total_count = total_count;

			if (parent_window != null)
				this.TransientFor = parent_window;

			HasSeparator = false;
			BorderWidth = 6;
			SetDefaultSize (300, -1);

			Message = new Label (String.Empty);
			VBox.PackStart (Message, true, true, 12);

			Bar = new ProgressBar ();
			VBox.PackStart (Bar, true, true, 6);

			switch (cancel_button_type) {
			case CancelButtonType.Cancel:
				Button = (Gtk.Button)AddButton (Gtk.Stock.Cancel, (int) ResponseType.Cancel);
				break;
			case CancelButtonType.Stop:
				Button = (Gtk.Button)AddButton (Gtk.Stock.Stop, (int) ResponseType.Cancel);
				break;
			}

			Response += new ResponseHandler (HandleResponse);
		}

		private int current_count;

		// Return true if the operation was cancelled by the user.
		public bool Update (string message)
		{
			current_count ++;

			Message.Text = message;
			Bar.Text = String.Format (Catalog.GetString ("{0} of {1}"), current_count, total_count);
			Bar.Fraction = (double) current_count / total_count;

			ShowAll ();

			while (Application.EventsPending ())
				Application.RunIteration ();

			return cancelled;
		}
	}
}
