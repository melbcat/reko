﻿/* 
 * Copyright (C) 1999-2010 John Källén.
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

using Decompiler.Core;
using Decompiler.Core.Machine;
using Decompiler.Core.Serialization;
using Decompiler.Core.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Decompiler.Gui
{
    public class SignatureParser
    {
        private IProcessorArchitecture arch;
        private string str;
        private int idx;

        public SignatureParser(IProcessorArchitecture arch)
        {
            this.arch = arch;
        }

        public void Parse(string signatureString)
        {
            this.str = signatureString;
            idx = 0;
            try
            {
                var ret = ParseReturn();
                var procName = ParseProcedureName();
                if (procName == null)
                    return;
                var procArgs = ParseProcedureArgs();
                if (procArgs == null)
                    return;

                ProcedureName = procName;
                Signature = new SerializedSignature();
                Signature.ReturnValue = ret;
                Signature.Arguments = procArgs;
                IsValid = true;
            }
            catch
            {
                IsValid = false;
            }
        }

        public static string UnparseSignature(SerializedSignature sig, string procName)
        {
            StringBuilder sb = new StringBuilder();
            if (sig.ReturnValue == null)
                sb.Append("void");
            else
                sb.Append(sig.ReturnValue.Name);
            sb.Append(" ");
            sb.Append(procName);
            sb.Append("(");
            string sep = "";
            foreach (var arg in sig.Arguments)
            {
                sb.Append(sep);
                sep = ", ";
                sb.Append(arg.Type);
                sb.Append(" ");
                sb.Append(arg.Name);
            }
            sb.Append(")");
            return sb.ToString();
        }


        public string ProcedureName { get; private set;}

        public SerializedSignature Signature { get; private set; }

        public bool IsValid { get; private set; }

        private SerializedArgument ParseReturn()
        {
            string w = GetNextWord();
            if (w == "void")
                return null;
            Console.WriteLine(w);
            MachineRegister reg;
            if (arch.TryGetRegister(w, out reg))
            {
                return new SerializedArgument(
                    reg.Name,
                    null,
                    new SerializedRegister(reg.Name),
                    true);
            }
            if (w.Contains("_"))
            {
                return ParseRegisterSequenceWithUnderscore(w);
            }
            throw new NotImplementedException();
        }

        private SerializedArgument ParseRegisterSequenceWithUnderscore(string w)
        {
            string[] subregs = w.Split('_');
            var regs = new List<SerializedRegister>();
            foreach (string subReg in subregs)
            {
                MachineRegister r;
                if (!arch.TryGetRegister(subReg, out r))
                    return null;
                regs.Add(new SerializedRegister(r.Name));
            }
            var seq = new SerializedSequence();
            seq.Registers = regs.ToArray();
            return new SerializedArgument(
                w,
                null,
                seq,
                true);
        }

        private string ParseProcedureName()
        {
            return GetNextWord();
        }

        private SerializedArgument[] ParseProcedureArgs()
        {
            EatWhiteSpace();
            if (idx >= str.Length || str[idx] != '(')
                return null;
            ++idx;
            var args = new List<SerializedArgument>();
            for (; ; )
            {
                EatWhiteSpace();
                if (idx >= str.Length)
                    return null;
                if (str[idx] == ')')
                    return args.ToArray();
                if (args.Count > 0)
                {
                    if (str[idx] != ',')
                        return null;
                    ++idx;
                }
                var arg = ParseArg();
                if (arg == null)
                    return null;
                args.Add(arg);
            }
        }

        private SerializedArgument ParseArg()
        {
            var w = GetNextWord();
            if (w == null)
                return null;

            MachineRegister reg;
            string type = null;
            if (!arch.TryGetRegister(w, out reg))
            {
                type = w;
                w = GetNextWord();
                if (w == null)
                    return null;
                if (w.Contains("_"))
                {
                    var retval = ParseRegisterSequenceWithUnderscore(w);
                    if (retval != null)
                        return retval;
                }
                if (!arch.TryGetRegister(w, out reg))
                {
                    return ParseStackArgument(type, w);
                }
            }

            if (PeekChar(':') || PeekChar('_'))
            {
                return ParseRegisterSequence(reg, type);
            }

            var arg = new SerializedArgument();
            arg.Name = reg.Name;
            arg.Kind = new SerializedRegister(reg.Name);
            arg.OutParameter = false;
            arg.Type = type;
            return arg;
        }

        private SerializedArgument ParseRegisterSequence(MachineRegister reg, string type)
        {
            ++idx;
            string w2 = GetNextWord();
            if (w2 == null)
                return null;
            MachineRegister reg2;
            if (!arch.TryGetRegister(w2, out reg2))
                return null;
            var seqArgName = reg.Name + "_" + reg2.Name;
            var seqArgType = type;
            var seqKind = new SerializedSequence();
            seqKind.Registers = new SerializedRegister[]{ 
                    new SerializedRegister(reg.Name), 
                    new SerializedRegister(reg2.Name)
                };
            return new SerializedArgument(seqArgName, seqArgType, seqKind, false);
        }

        private bool PeekChar(char cha)
        {
            return idx < str.Length && str[idx] == cha;
        }

        private SerializedArgument ParseStackArgument(string typeName, string argName)
        {
            PrimitiveType p;
            int sizeInWords;
            int wordSize = arch.WordWidth.Size;
            if (PrimitiveType.TryParse(typeName, out p))
            {
                sizeInWords = (p.Size + (wordSize - 1))/wordSize;
            }
            else
            {
                sizeInWords = 1;      // A reasonable guess, but is it a good one?
            }

            SerializedArgument arg = new SerializedArgument();
            arg.Name= argName;
            arg.Type = typeName;
            arg.Kind = new SerializedStackVariable(sizeInWords * wordSize);
            return arg;
        }

        private string GetNextWord()
        {
            EatWhiteSpace();
            if (idx >= str.Length)
                return null;

            int i = idx;
            char c = str[i];
            while (i < str.Length && (Char.IsLetterOrDigit(c) || c == '_'))
            {
                ++i;
                c = str[i];
            }
            string s = str.Substring(idx, i - idx);
            idx = i;
            return s;
        }

        private void EatWhiteSpace()
        {
            while (idx < str.Length && Char.IsWhiteSpace(str[idx]))
                ++idx;
        }
    }
}