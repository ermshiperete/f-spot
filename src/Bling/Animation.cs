//
// FSpot.Bling.Animation,cs
//
// Author(s):
//	Stephane Delcroix  <stephane@delcroix.org>
//
// This is free software. See COPYING for details
//

//TODO: send the progresschanged event

using System;
using System.ComponentModel;

namespace FSpot.Bling
{
	public abstract class Animation<T>
	{
		enum AnimationState {
			NotRunning = 0,
			Running,
			Paused,
		}

		TimeSpan duration;
		TimeSpan pausedafter;

		DateTimeOffset starttime;
		T from;
		T to;
		Action<T> action;
		AnimationState state;

		public Animation ()
		{
			from = default (T);
			to = default (T);
			duration = TimeSpan.Zero;
			action = null;
			state = AnimationState.NotRunning;
		}

		public Animation (T from, T to, TimeSpan duration, Action<T> action)
		{
			this.from = from;
			this.to = to;
			this.duration = duration;
			this.action = action;
			state = AnimationState.NotRunning;
		}

		public void Pause ()
		{
			if (state == AnimationState.Paused)
				return;
			if (state != AnimationState.NotRunning)
				throw new InvalidOperationException ("Can't Pause () a non running animation");
			GLib.Idle.Remove (Handler);
			pausedafter = DateTimeOffset.Now - starttime;
			state = AnimationState.Paused;
		}

		public void Resume ()
		{
			if (state == AnimationState.Running)
				return;
			if (state != AnimationState.Paused)
				throw new InvalidOperationException ("Can't Resume a non running animation");
			starttime = DateTimeOffset.Now - pausedafter;
			state = AnimationState.Running;
			GLib.Timeout.Add (40, Handler, GLib.Priority.DefaultIdle);
		}

		public void Start ()
		{
			if (state != AnimationState.NotRunning)
				throw new InvalidOperationException ("Can't Start() a running or paused animation");
			starttime = DateTimeOffset.Now;
			state = AnimationState.Running;
			GLib.Timeout.Add (40, Handler, GLib.Priority.DefaultIdle);
		}

		public void Stop ()
		{
			if (state == AnimationState.NotRunning)
				return;
			else
				GLib.Idle.Remove (Handler);
			action (to);
			EventHandler h = Completed;
			if (h != null)
				Completed (this, EventArgs.Empty);
			state = AnimationState.NotRunning;
		}

		public void Restart ()
		{
			if (state == AnimationState.NotRunning) {
				Start ();
				return;
			}
			if (state == AnimationState.Paused) {
				Resume ();
				return;
			}
			starttime = DateTimeOffset.Now;
		}

		protected virtual double Ease (double normalizedTime)
		{
			return normalizedTime;
		}

		bool Handler ()
		{
			double percentage = (double)(DateTimeOffset.Now - starttime ).Ticks / (double)duration.Ticks;
			action (Interpolate (from, to, Ease (Math.Min (percentage, 1.0))));
			if (percentage >= 1.0) {
				EventHandler h = Completed;
				if (h != null)
					Completed (this, EventArgs.Empty);
				state = AnimationState.NotRunning;
				return false;
			}
			return true;
		}

		protected abstract T Interpolate (T from, T to, double progress);

		public event EventHandler Completed;
		public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

		public bool IsRunning {
			get { return state == AnimationState.Running; }
		}

		public bool IsPaused {
			get { return state == AnimationState.Paused; }
		}

		public TimeSpan Duration {
			get { return duration; }
			set { duration = value; }
		}

		public T From {
			get { return from; }
			set { from = value; }
		}

		public T To {
			get { return to; }
			set { to = value; }
		}
	}
}
