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
using System.Diagnostics;
using System.Linq;

namespace Steelbreeze.Behavior
{
	/// <summary>
	/// Common base class for all transition types
	/// </summary>
	public abstract class TransitionBase
	{
		readonly internal Action<ITransaction> exit;
		readonly internal Action<ITransaction> enter;
		readonly internal Action<ITransaction, Boolean> complete;

		internal TransitionBase( Vertex source, Vertex target )
		{
			if( target != null )
			{
				var sourceAncestors = Ancestors( source ).Reverse().GetEnumerator();
				var targetAncestors = Ancestors( target ).Reverse().GetEnumerator();

				while( sourceAncestors.MoveNext() && targetAncestors.MoveNext() && sourceAncestors.Current.Equals( targetAncestors.Current ) ) { }

				if( source is PseudoState && !sourceAncestors.Current.Equals( source ) )
					exit += source.OnExit;

				exit += sourceAncestors.Current.OnExit;

				do { enter += targetAncestors.Current.BeginEnter; } while( targetAncestors.MoveNext() );

				complete += target.EndEnter;
			}
		}

		private static IEnumerable<StateMachineBase> Ancestors( Vertex vertex )
		{
			while( vertex != null )
			{
				yield return vertex;

				if( vertex.Parent == null )
					break;

				yield return vertex.Parent;

				vertex = vertex.Parent.Parent;
			}
		}
	}
}