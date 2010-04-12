/* 
 * Copyright (C) 1999-2010 John K�ll�n.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using Decompiler.Analysis;
using Decompiler.Core;
using NUnit.Framework;
using Decompiler.UnitTests.Mocks;
using System;

namespace Decompiler.UnitTests.Analysis
{
	[TestFixture]
	public class GrfDefinitionFinderTests : AnalysisTestBase
	{
		[Test]
		public void GrfdAdcMock()
		{
			RunTest(new AdcMock(), "Analysis/GrfdAdcMock.txt");
		}

		[Test]
		public void GrfdAddSubCarries()
		{
			RunTest("Fragments/addsubcarries.asm", "Analysis/GrfdAddSubCarries.txt");
		}

		[Test]
		public void GrfdCmpMock()
		{
			RunTest(new CmpMock(), "Analysis/GrfdCmpMock.txt");
		}

		protected override void RunTest(Program prog, FileUnitTester fut)
		{
            DataFlowAnalysis dfa = new DataFlowAnalysis(prog, new FakeDecompilerEventListener());
			dfa.UntangleProcedures();
			foreach (Procedure proc in prog.Procedures.Values)
			{
				Aliases alias = new Aliases(proc, prog.Architecture);
				alias.Transform();
				SsaTransform sst = new SsaTransform(proc, new DominatorGraph(proc), true);
				SsaState ssa = sst.SsaState;
				GrfDefinitionFinder grfd = new GrfDefinitionFinder(ssa.Identifiers);
				foreach (SsaIdentifier sid in ssa.Identifiers)
				{
					if (!(sid.OriginalIdentifier.Storage is FlagGroupStorage) || sid.Uses.Count == 0)
						continue;
					fut.TextWriter.Write("{0}: ", sid.DefStatement.Instruction);
					grfd.FindDefiningExpression(sid);
					string fmt = grfd.IsNegated ? "!{0};" : "{0}";
					fut.TextWriter.WriteLine(fmt, grfd.DefiningExpression);
				}
			}
		}
	}
}