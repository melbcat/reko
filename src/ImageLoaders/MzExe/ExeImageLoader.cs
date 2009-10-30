/* 
 * Copyright (C) 1999-2009 John K�ll�n.
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

using Decompiler.Arch.Intel;
using Decompiler.Core;
using Decompiler.Environments.Msdos;
using System;
using System.Collections.Generic;

namespace Decompiler.ImageLoaders.MzExe
{
	/// <summary>
	/// Loads Microsoft EXE image files. 
	/// </summary>
	public class ExeImageLoader : ImageLoader
	{
        private ImageLoader ldrDeferred;

		public ushort	e_magic;                     // Magic number
		public ushort   e_cbLastPage;                // Bytes on last page of file
		public ushort   e_cpImage;                   // Pages in file
		public ushort   e_cRelocations;              // Relocations

		public ushort   e_cparHeader;                // Size of header in paragraphs
		public ushort   e_minalloc;                  // Minimum extra paragraphs needed
		public ushort   e_maxalloc;                  // Maximum extra paragraphs needed
		public ushort   e_ss;                        // Initial (relative) SS value

		public ushort   e_sp;                        // Initial SP value
		public ushort   e_csum;                      // Checksum
		public ushort   e_ip;                        // Initial IP value
		public ushort   e_cs;                        // Initial (relative) CS value

		public ushort   e_lfaRelocations;			 // File address of relocation table
		public ushort   e_ovno;                      // Overlay number
		public ushort [] e_res;                      // Reserved words
		public ushort   e_oemid;                     // OEM identifier (for e_oeminfo)
		public ushort   e_oeminfo;                   // OEM information; e_oemid specific
		public ushort [] e_res2;                     // Reserved words
		public uint      e_lfanew;                    // File address of new exe header

		private const int MarkZbikowski = (('Z' << 8) | 'M');		// 'MZ' magic number expressed in little-endian.

		public const int CbPsp = 0x0100;			// Program segment prefix size in bytes.
		public const int CbPageSize = 0x0200;		// MSDOS pages are 512 bytes.

        private Program prog;

		public ExeImageLoader(Program prog, byte [] image) : base(prog, image)
		{
            this.prog = prog;
            ReadCommonExeFields();	
		
			if (e_magic != MarkZbikowski)
				throw new FormatException("Image is not an MS-DOS executable image.");
		}

        private ImageLoader CreateRealModeLoader(Program prog, byte[] image)
        {
            IntelArchitecture arch = new IntelArchitecture(ProcessorMode.Real);
            prog.Architecture = arch;
            prog.Platform = new MsdosPlatform(arch);

            if (LzExeUnpacker.IsCorrectUnpacker(this, image))
            {
                return new LzExeUnpacker(prog, this, image);
            }
            else if (PkLiteUnpacker.IsCorrectUnpacker(this, image))
            {
                return new PkLiteUnpacker(prog, this, image);
            }
            else if (ExePackLoader.IsCorrectUnpacker(this, image))
            {
                return new ExePackLoader(prog, this, image);
            }
            else
            {
                return new MsdosImageLoader(prog, this);
            }
        }

        private ImageLoader DeferredLoader
        {
            get
            {
                if (ldrDeferred == null)
                    ldrDeferred = CreateDeferredLoader();
                return ldrDeferred;
            }
        }

		public bool IsNewExecutable
		{
			get { return (uint) RawImage.Length > (uint) (e_lfanew + 1) && RawImage[e_lfanew] == 'N' && RawImage[e_lfanew+1] == 'E'; }
		}

		public bool IsRealModeExecutable
		{
			get { return e_lfanew == 0; }
		}

		public bool IsPortableExecutable
		{
			get { return (uint) RawImage.Length > (uint) (e_lfanew + 1) && RawImage[e_lfanew] == 'P' && RawImage[e_lfanew+1] == 'E'; }
		}


		/// <summary>
		/// Loads a Microsoft .EXE file. There are several widely varying sub-formats,
		/// so we need to discover what flavour it is before we can proceed.
		/// </summary>
		public override ProgramImage Load(Address addrLoad)
		{


			return DeferredLoader.Load(addrLoad);
		}

        private ImageLoader CreateDeferredLoader()
        {
            if (IsPortableExecutable)
            {
                return new PeImageLoader(prog, base.RawImage, e_lfanew);
            }
            else if (IsNewExecutable)
            {
                prog.Architecture = new IntelArchitecture(ProcessorMode.ProtectedSegmented);
                throw new NotImplementedException("NE executable loading not implemented.");
            }
            else
            {
                return CreateRealModeLoader(prog, RawImage);
            }
        }

        public override ProgramImage LoadAtPreferredAddress()
        {
            return Load(DeferredLoader.PreferredBaseAddress);
        }

		public override Address PreferredBaseAddress
		{
			get { return DeferredLoader.PreferredBaseAddress; }
		}

		public void ReadCommonExeFields()
		{
			ImageReader rdr = new ImageReader(RawImage, 0);

			e_magic = rdr.ReadLeUint16();        
			e_cbLastPage = rdr.ReadLeUint16();         
			e_cpImage = rdr.ReadLeUint16();           
			this.e_cRelocations = rdr.ReadLeUint16();         
			e_cparHeader = rdr.ReadLeUint16();      
			e_minalloc = rdr.ReadLeUint16();     
			e_maxalloc = rdr.ReadLeUint16();     
			e_ss = rdr.ReadLeUint16();           
			e_sp = rdr.ReadLeUint16();           
			e_csum = rdr.ReadLeUint16();         
			e_ip = rdr.ReadLeUint16();              
			e_cs = rdr.ReadLeUint16();              
			e_lfaRelocations = rdr.ReadLeUint16();          
			e_ovno = rdr.ReadLeUint16();            
			e_res = new ushort[4];
			for (int i = 0; i != 4; ++i)
			{
				e_res[i] = rdr.ReadLeUint16();          
			}
			e_oemid = rdr.ReadLeUint16();           
			e_oeminfo = rdr.ReadLeUint16();         
			e_res2 = new ushort[10];
			for (int i = 0; i != 10; ++i)
			{
				e_res2[i] = rdr.ReadLeUint16();        
			}
			e_lfanew = rdr.ReadLeUint32();          
		}

		public override void Relocate(Address addrLoad, List<EntryPoint> entryPoints, RelocationDictionary relocations)
		{
			DeferredLoader.Relocate(addrLoad, entryPoints, relocations);
		}

		void RelocateNewExe(object neHeader)
		{
		}
	}
}