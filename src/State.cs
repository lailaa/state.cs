﻿// Copyright © 2013 Steelbreeze Limited.
// This file is part of state.cs.
//
// state.cs is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License,
// or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Steelbreeze.Behavior
{
	/// <summary>
	/// A condition or situation during the life of an object during which it satisfies some condition, performs some activity, or waits for some event.
	/// </summary>
	public class State : StateBase
	{
		/// <summary>
		/// Returns the default Region within a CompositeState.
		/// </summary>
		/// <param name="state">The CompositeState to find the default Region for.</param>
		/// <returns>The default Region.</returns>
		public static implicit operator Region( State state ) { return state.Default; }

		internal HashSet<Transition> completions = null;
		internal HashSet<TypedTransition> transitions = null;
		internal HashSet<Region> regions = null;
		private Region defaultRegion = null;
		private Object sync = new Object();

		/// <summary>
		/// The default region used for composite states when no region is explicitly referenced.
		/// </summary>
		public Region Default { get { return defaultRegion ?? ( defaultRegion = new Region( "default", this ) ); } }

		/// <summary>
		/// The action or actions performed when entering a State.
		/// </summary>
		public event Action Entry;

		/// <summary>
		/// The action or actions performed when leaving a State.
		/// </summary>
		public event Action Exit;

		/// <summary>
		/// True if the state has no child regions
		/// </summary>
		public Boolean IsSimple { get { return regions == null; } }

		/// <summary>
		/// True if the state has child regions
		/// </summary>
		public Boolean IsComposite { get { return regions != null; } }

		/// <summary>
		/// True if the state has more than one child region
		/// </summary>
		public Boolean IsOrthogonal { get { return IsComposite && regions.Count() > 1; } }

		/// <summary>
		/// True when the State is completed.
		/// </summary>
		public Boolean IsComplete { get { return IsSimple || regions.All( region => region.IsComplete ); } }

		/// <summary>
		/// Creates a State.
		/// </summary>
		/// <param name="name">The name of the State.</param>
		/// <param name="parent">The parent Region or the State.</param>
		public State( String name, Region parent = null ) : base( name, parent ) { }

		/// <summary>
		/// Initialises a node to its initial state.
		/// </summary>
		/// <param name="transaction">An optional transaction that the process operation will participate in.</param>
		public void Initialise( ITransaction transaction = null )
		{
			lock( sync )
			{
				var transactionOwner = transaction == null;

				if( transactionOwner )
					transaction = TransactionManager.Default();

				Initialise( transaction, false );

				if( transactionOwner )
					transaction.Commit();
			}
		}

		override internal void OnExit( ITransaction transaction )
		{
			if( IsComposite )
				foreach( var region in regions.Where( r => r.IsActive ) )
					region.OnExit( transaction );

			if( Exit != null )
				Exit();

			base.OnExit( transaction );
		}

		override internal void BeginEnter( ITransaction transaction )
		{
			base.BeginEnter( transaction );

			if( Entry != null )
				Entry();
		}

		internal override void EndEnter( ITransaction transaction, bool deepHistory )
		{
			if( IsComposite )
				foreach( var region in regions )
					region.Initialise( transaction, deepHistory );

			if( completions != null )
			{
				if( IsSimple || regions.All( r => transaction.GetCurrent( r ) is FinalState ) ) // IsComplete for in-flight transactions
				{
					var completion = completions.SingleOrDefault( c => c.guard() );

					if( completion != null )
						completion.Traverse( transaction, deepHistory );
				}
			}
		}

		/// <summary>
		/// Attempts to process a message.
		/// </summary>
		/// <param name="message">The message to process.</param>
		/// <param name="transaction">An optional transaction that the process operation will participate in.</param>
		/// <returns>A Boolean indicating if the message was processed.</returns>
		override public Boolean Process( Object message, ITransaction transaction = null )
		{
			lock( sync )
			{
				var transactionOwner = transaction == null;

				if( transactionOwner )
					transaction = TransactionManager.Default();

				var transition = transitions == null ? null : transitions.SingleOrDefault( t => t.Guard( message ) );
				var processed = transition != null;

				if( processed )
				{
					transition.Traverse( transaction, message );

					transaction.Commit();
				}
				else
				{
					if( IsComposite )
						foreach( var region in regions.Where( r => r.IsActive ) )
							processed |= region.Process( message );
				}

				if( transactionOwner )
					transaction.Commit();

				return processed;
			}
		}
		
		/// <summary>
		/// Accepts a Visitor object and visits all child Regions.
		/// </summary>
		/// <typeparam name="TContext">The type of the context to pass while visiting the CompositeState.</typeparam>
		/// <param name="visitor">The Visitor object.</param>
		/// <param name="context">The context to pass while visiting the CompositeState.</param>
		/// <returns>Context to pass on to sibling Vertics within the parent Region.</returns>
		override public TContext Accept<TContext>( Visitor<TContext> visitor, TContext context = default( TContext ) )
		{
			context = visitor.Visit( this, base.Accept( visitor, context ) );

			if( IsComposite )
				foreach( var region in regions )
					region.Accept( visitor, context );

			return context;
		}
	}
}