using System;
using System.Collections.Generic;

namespace CellDotNet
{
	// This enumeration is generated by CellDotNet.CodeGenUtils. DO NOT EDIT.
	enum SpuOpCodeEnum
	{
		None,
		/// <summary>
		/// Load Quadword (d-form)
		/// </summary>
		Lqd,
		/// <summary>
		/// Load Quadword (x-form)
		/// </summary>
		Lqx,
		/// <summary>
		/// Load Quadword (a-form)
		/// </summary>
		Lqa,
		/// <summary>
		/// Load Quadword Instruction Relative (a-form)
		/// </summary>
		Lqr,
		/// <summary>
		/// Store Quadword (d-form)
		/// </summary>
		Stqd,
		/// <summary>
		/// Store Quadword (x-form)
		/// </summary>
		Stqx,
		/// <summary>
		/// Store Quadword (a-form)
		/// </summary>
		Stqa,
		/// <summary>
		/// Store Quadword Instruction Relative (a-form)
		/// </summary>
		Stqr,
		/// <summary>
		/// Generate Controls for Byte Insertion (d-form)
		/// </summary>
		Cbd,
		/// <summary>
		/// Generate Controls for Byte Insertion (x-form)
		/// </summary>
		Cbx,
		/// <summary>
		/// Generate Controls for Halfword Insertion (d-form)
		/// </summary>
		Chd,
		/// <summary>
		/// Generate Controls for Halfword Insertion (x-form)
		/// </summary>
		Chx,
		/// <summary>
		/// Generate Controls for Word Insertion (d-form)
		/// </summary>
		Cwd,
		/// <summary>
		/// Generate Controls for Word Insertion (x-form)
		/// </summary>
		Cwx,
		/// <summary>
		/// Generate Controls for Doubleword Insertion (d-form)
		/// </summary>
		Cdd,
		/// <summary>
		/// Generate Controls for Doubleword Insertion (x-form)
		/// </summary>
		Cdx,
		/// <summary>
		/// Immediate Load Halfword
		/// </summary>
		Ilh,
		/// <summary>
		/// Immediate Load Halfword Upper
		/// </summary>
		Ilhu,
		/// <summary>
		/// Immediate Load Word
		/// </summary>
		Il,
		/// <summary>
		/// Immediate Load Address
		/// </summary>
		Ila,
		/// <summary>
		/// Immediate Or Halfword Lower
		/// </summary>
		Iohl,
		/// <summary>
		/// Form Select Mask for Bytes Immediate
		/// </summary>
		Fsmbi,
		/// <summary>
		/// Add Halfword
		/// </summary>
		Ah,
		/// <summary>
		/// Add Halfword Immediate
		/// </summary>
		Ahi,
		/// <summary>
		/// Add Word
		/// </summary>
		A,
		/// <summary>
		/// Add Word Immediate
		/// </summary>
		Ai,
		/// <summary>
		/// Subtract from Halfword
		/// </summary>
		Sfh,
		/// <summary>
		/// Subtract from Halfword Immediate
		/// </summary>
		Sfhi,
		/// <summary>
		/// Subtract from Word
		/// </summary>
		Sf,
		/// <summary>
		/// Subtract from Word Immediate
		/// </summary>
		Sfi,
		/// <summary>
		/// Add Extended
		/// </summary>
		Addx,
		/// <summary>
		/// Carry Generate
		/// </summary>
		Cg,
		/// <summary>
		/// Carry Generate Extended
		/// </summary>
		Cgx,
		/// <summary>
		/// Subtract from Extended
		/// </summary>
		Sfx,
		/// <summary>
		/// Borrow Generate
		/// </summary>
		Bg,
		/// <summary>
		/// Borrow Generate Extended
		/// </summary>
		Bgx,
		/// <summary>
		/// Multiply
		/// </summary>
		Mpy,
		/// <summary>
		/// Multiply Unsigned
		/// </summary>
		Mpyu,
		/// <summary>
		/// Multiply Immediate
		/// </summary>
		Mpyi,
		/// <summary>
		/// Multiply Unsigned Immediate
		/// </summary>
		Mpyui,
		/// <summary>
		/// Multiply and Add
		/// </summary>
		Mpya,
		/// <summary>
		/// Multiply High
		/// </summary>
		Mpyh,
		/// <summary>
		/// Multiply and Shift Right
		/// </summary>
		Mpys,
		/// <summary>
		/// Multiply High High
		/// </summary>
		Mpyhh,
		/// <summary>
		/// Multiply High High and Add
		/// </summary>
		Mpyhha,
		/// <summary>
		/// Multiply High High Unsigned
		/// </summary>
		Mpyhhu,
		/// <summary>
		/// Multiply High High Unsigned and Add
		/// </summary>
		Mpyhhau,
		/// <summary>
		/// Count Leading Zeros
		/// </summary>
		Clz,
		/// <summary>
		/// Count Ones in Bytes
		/// </summary>
		Cntb,
		/// <summary>
		/// Form Select Mask for Bytes
		/// </summary>
		Fsmb,
		/// <summary>
		/// Form Select Mask for Halfwords
		/// </summary>
		Fsmh,
		/// <summary>
		/// Form Select Mask for Words
		/// </summary>
		Fsm,
		/// <summary>
		/// Gather Bits from Bytes
		/// </summary>
		Gbb,
		/// <summary>
		/// Gather Bits from Halfwords
		/// </summary>
		Gbh,
		/// <summary>
		/// Gather Bits from Words
		/// </summary>
		Gb,
		/// <summary>
		/// Average Bytes
		/// </summary>
		Avgb,
		/// <summary>
		/// Absolute Differences of Bytes
		/// </summary>
		Absdb,
		/// <summary>
		/// Sum Bytes into Halfwords
		/// </summary>
		Sumb,
		/// <summary>
		/// Extend Sign Byte to Halfword
		/// </summary>
		Xsbh,
		/// <summary>
		/// Extend Sign Halfword to Word
		/// </summary>
		Xshw,
		/// <summary>
		/// Extend Sign Word to Doubleword
		/// </summary>
		Xswd,
		/// <summary>
		/// And
		/// </summary>
		And,
		/// <summary>
		/// And with Complement
		/// </summary>
		Andc,
		/// <summary>
		/// And Byte Immediate
		/// </summary>
		Andbi,
		/// <summary>
		/// And Halfword Immediate
		/// </summary>
		Andhi,
		/// <summary>
		/// And Word Immediate
		/// </summary>
		Andi,
		/// <summary>
		/// Or
		/// </summary>
		Or,
		/// <summary>
		/// Or with Complement
		/// </summary>
		Orc,
		/// <summary>
		/// Or Byte Immediate
		/// </summary>
		Orbi,
		/// <summary>
		/// Or Halfword Immediate
		/// </summary>
		Orhi,
		/// <summary>
		/// Or Word Immediate
		/// </summary>
		Ori,
		/// <summary>
		/// Or Across
		/// </summary>
		Orx,
		/// <summary>
		/// Exclusive Or
		/// </summary>
		Xor,
		/// <summary>
		/// Exclusive Or Byte Immediate
		/// </summary>
		Xorbi,
		/// <summary>
		/// Exclusive Or Halfword Immediate
		/// </summary>
		Xorhi,
		/// <summary>
		/// Exclusive Or Word Immediate
		/// </summary>
		Xori,
		/// <summary>
		/// Nand
		/// </summary>
		Nand,
		/// <summary>
		/// Nor
		/// </summary>
		Nor,
		/// <summary>
		/// Equivalent
		/// </summary>
		Eqv,
		/// <summary>
		/// Select Bits
		/// </summary>
		Selb,
		/// <summary>
		/// Shuffle Bytes
		/// </summary>
		Shufb,
		/// <summary>
		/// Shift Left Halfword
		/// </summary>
		Shlh,
		/// <summary>
		/// Shift Left Halfword Immediate
		/// </summary>
		Shlhi,
		/// <summary>
		/// Shift Left Word
		/// </summary>
		Shl,
		/// <summary>
		/// Shift Left Word Immediate
		/// </summary>
		Shli,
		/// <summary>
		/// Shift Left Quadword by Bits
		/// </summary>
		Shlqbi,
		/// <summary>
		/// Shift Left Quadword by Bits Immediate
		/// </summary>
		Shlqbii,
		/// <summary>
		/// Shift Left Quadword by Bytes
		/// </summary>
		Shlqby,
		/// <summary>
		/// Shift Left Quadword by Bytes Immediate
		/// </summary>
		Sqlqbyi,
		/// <summary>
		/// Shift Left Quadword by Bytes from Bit Shift Count
		/// </summary>
		Shlqbybi,
		/// <summary>
		/// Rotate Halfword
		/// </summary>
		Roth,
		/// <summary>
		/// Rotate Halfword Immediate
		/// </summary>
		Rothi,
		/// <summary>
		/// Rotate Word
		/// </summary>
		Rot,
		/// <summary>
		/// Rotate Word Immediate
		/// </summary>
		Roti,
		/// <summary>
		/// Rotate Quadword by Bytes
		/// </summary>
		Rotqby,
		/// <summary>
		/// Rotate Quadword by Bytes Immediate
		/// </summary>
		Rotqbyi,
		/// <summary>
		/// Rotate Quadword by Bytes from Bit Shift Count
		/// </summary>
		Rotqbybi,
		/// <summary>
		/// Rotate Quadword by Bits
		/// </summary>
		Rotqbi,
		/// <summary>
		/// Rotate Quadword by Bits Immediate
		/// </summary>
		Rotqbii,
		/// <summary>
		/// Rotate and Mask Halfword
		/// </summary>
		Rothm,
		/// <summary>
		/// Rotate and Mask Halfword Immediate
		/// </summary>
		Rothmi,
		/// <summary>
		/// Rotate and Mask Word
		/// </summary>
		Rotm,
		/// <summary>
		/// Rotate and Mask Word Immediate
		/// </summary>
		Rotmi,
		/// <summary>
		/// Rotate and Mask Quadword by Bytes
		/// </summary>
		Rotqmby,
		/// <summary>
		/// Rotate and Mask Quadword by Bytes Immediate
		/// </summary>
		Rotqmbyi,
		/// <summary>
		/// Rotate and Mask Quadword Bytes from Bit Shift Count
		/// </summary>
		Rotqmbybi,
		/// <summary>
		/// Rotate and Mask Quadword by Bits
		/// </summary>
		Rotqmbi,
		/// <summary>
		/// Rotate and Mask Quadword by Bits Immediate
		/// </summary>
		Rotqmbii,
		/// <summary>
		/// Rotate and Mask Algebraic Halfword
		/// </summary>
		Rotmah,
		/// <summary>
		/// Rotate and Mask Algebraic Halfword Immediate
		/// </summary>
		Rotmahi,
		/// <summary>
		/// Rotate and Mask Algebraic Word
		/// </summary>
		Rotma,
		/// <summary>
		/// Rotate and Mask Algebraic Word Immediate
		/// </summary>
		Rotmai,
		/// <summary>
		/// Halt If Equal
		/// </summary>
		Heq,
		/// <summary>
		/// Halt If Equal Immediate
		/// </summary>
		Heqi,
		/// <summary>
		/// Halt If Greater Than
		/// </summary>
		Hgt,
		/// <summary>
		/// Halt If Greater Than Immediate
		/// </summary>
		Hgti,
		/// <summary>
		/// Halt If Logically Greater Than
		/// </summary>
		Hlgt,
		/// <summary>
		/// Halt If Logically Greater Than Immediate
		/// </summary>
		Hlgti,
		/// <summary>
		/// Compare Equal Byte
		/// </summary>
		Ceqb,
		/// <summary>
		/// Compare Equal Byte Immediate
		/// </summary>
		Ceqbi,
		/// <summary>
		/// Compare Equal Halfword
		/// </summary>
		Ceqh,
		/// <summary>
		/// Compare Equal Halfword Immediate
		/// </summary>
		Ceqhi,
		/// <summary>
		/// Compare Equal Word
		/// </summary>
		Ceq,
		/// <summary>
		/// Compare Equal Word Immediate
		/// </summary>
		Ceqi,
		/// <summary>
		/// Compare Greater Than Byte
		/// </summary>
		Cgtb,
		/// <summary>
		/// Compare Greater Than Byte Immediate
		/// </summary>
		Cgtbi,
		/// <summary>
		/// Compare Greater Than Halfword
		/// </summary>
		Cgth,
		/// <summary>
		/// Compare Greater Than Halfword Immediate
		/// </summary>
		Cgthi,
		/// <summary>
		/// Compare Greater Than Word
		/// </summary>
		Cgt,
		/// <summary>
		/// Compare Greater Than Word Immediate
		/// </summary>
		Cgti,
		/// <summary>
		/// Compare Logical Greater Than Byte
		/// </summary>
		Clgtb,
		/// <summary>
		/// Compare Logical Greater Than Byte Immediate
		/// </summary>
		Clgtbi,
		/// <summary>
		/// Compare Logical Greater Than Halfword
		/// </summary>
		Clgth,
		/// <summary>
		/// Compare Logical Greater Than Halfword Immediate
		/// </summary>
		Clgthi,
		/// <summary>
		/// Compare Logical Greater Than Word
		/// </summary>
		Clgt,
		/// <summary>
		/// Compare Logical Greater Than Word Immediate
		/// </summary>
		Clgti,
		/// <summary>
		/// Branch Relative
		/// </summary>
		Br,
		/// <summary>
		/// Branch Absolute
		/// </summary>
		Bra,
		/// <summary>
		/// Branch Relative and Set Link
		/// </summary>
		Brsl,
		/// <summary>
		/// Branch Absolute and Set Link
		/// </summary>
		Brasl,
		/// <summary>
		/// Branch Indirect
		/// </summary>
		Bi,
		/// <summary>
		/// Interrupt Return
		/// </summary>
		Iret,
		/// <summary>
		/// Branch Indirect and Set Link if External Data
		/// </summary>
		Bisled,
		/// <summary>
		/// Branch Indirect and Set Link
		/// </summary>
		Bisl,
		/// <summary>
		/// Branch If Not Zero Word
		/// </summary>
		Brnz,
		/// <summary>
		/// Branch If Zero Word
		/// </summary>
		Brz,
		/// <summary>
		/// Branch If Not Zero Halfword
		/// </summary>
		Brhnz,
		/// <summary>
		/// Branch If Zero Halfword
		/// </summary>
		Brhz,
		/// <summary>
		/// Branch Indirect If Zero
		/// </summary>
		Biz,
		/// <summary>
		/// Branch Indirect If Not Zero
		/// </summary>
		Binz,
		/// <summary>
		/// Branch Indirect If Zero Halfword
		/// </summary>
		Bihz,
		/// <summary>
		/// Branch Indirect If Not Zero Halfword
		/// </summary>
		Bihnz,
		/// <summary>
		/// Floating Add
		/// </summary>
		Fa,
		/// <summary>
		/// Double Floating Add
		/// </summary>
		Dfa,
		/// <summary>
		/// Floating Subtract
		/// </summary>
		Fs,
		/// <summary>
		/// Double Floating Subtract
		/// </summary>
		Dfs,
		/// <summary>
		/// Floating Multiply
		/// </summary>
		Fm,
		/// <summary>
		/// Double Floating Multiply
		/// </summary>
		Dfm,
		/// <summary>
		/// Floating Multiply and Add
		/// </summary>
		Fma,
		/// <summary>
		/// Double Floating Multiply and Add
		/// </summary>
		Dfma,
		/// <summary>
		/// Floating Negative Multiply and Subtract
		/// </summary>
		Fnms,
		/// <summary>
		/// Double Floating Negative Multiply and Subtract
		/// </summary>
		Dfnms,
		/// <summary>
		/// Floating Multiply and Subtract
		/// </summary>
		Fms,
		/// <summary>
		/// Double Floating Multiply and Subtract
		/// </summary>
		Dfms,
		/// <summary>
		/// Double Floating Negative Multiply and Add
		/// </summary>
		Dfnma,
		/// <summary>
		/// Floating Reciprocal Estimate
		/// </summary>
		Frest,
		/// <summary>
		/// Floating Reciprocal Absolute Square Root Estimate
		/// </summary>
		Frsqest,
		/// <summary>
		/// Floating Interpolate
		/// </summary>
		Fi,
		/// <summary>
		/// Convert Signed Integer to Floating
		/// </summary>
		Csflt,
		/// <summary>
		/// Convert Floating to Signed Integer
		/// </summary>
		Cflts,
		/// <summary>
		/// Convert Unsigned Integer to Floating
		/// </summary>
		Cuflt,
		/// <summary>
		/// Convert Floating to Unsigned Integer
		/// </summary>
		Cfltu,
		/// <summary>
		/// Floating Round Double to Single
		/// </summary>
		Frds,
		/// <summary>
		/// Floating Extend Single to Double
		/// </summary>
		Fesd,
		/// <summary>
		/// Double Floating Compare Equal
		/// </summary>
		Dfceq,
		/// <summary>
		/// Double Floating Compare Magnitude Equal
		/// </summary>
		Dfcmeq,
		/// <summary>
		/// Double Floating Compare Greater Than
		/// </summary>
		Dfcgt,
		/// <summary>
		/// Double Floating Compare Magnitude Greater Than
		/// </summary>
		Dfcmgt,
		/// <summary>
		/// Double Floating Test Special Value
		/// </summary>
		Dftsv,
		/// <summary>
		/// Floating Compare Equal
		/// </summary>
		Fceq,
		/// <summary>
		/// Floating Compare Magnitude Equal
		/// </summary>
		Fcmeq,
		/// <summary>
		/// Floating Compare Greater Than
		/// </summary>
		Fcgt,
		/// <summary>
		/// Floating Compare Magnitude Greater Than
		/// </summary>
		Fcmgt,
		/// <summary>
		/// Floating-Point Status and Control Register Write
		/// </summary>
		Fscrwr,
		/// <summary>
		/// Floating-Point Status and Control Register Read
		/// </summary>
		Fscrrd,
		/// <summary>
		/// Stop and Signal
		/// </summary>
		Stop,
		/// <summary>
		/// No Operation (Load)
		/// </summary>
		Lnop,
		/// <summary>
		/// No Operation (Execute)
		/// </summary>
		Nop,
		/// <summary>
		/// Read Channel
		/// </summary>
		Rdch,
		/// <summary>
		/// Read Channel Count
		/// </summary>
		Rchcnt,
		/// <summary>
		/// Write Channel
		/// </summary>
		Wrch,
		/// <summary>
		/// Move (pseudo)
		/// </summary>
		Move,
		/// <summary>
		/// Function return (pseudo)
		/// </summary>
		Ret,
	}
}
