﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Andromeda
{
    public class TypeName
    {
        public string Type { get; private set; }
        public string Name { get; private set; }

        public TypeName(string t, string name)
        {
            this.Type = t;
            this.Name = name;
        }
    }

    public class Converter
    {
        private static string[] noop;
        private Token[] tokens;
        private Token cur;
        private Token last;
        private int pos;
        private string indent;
        private bool isNew;

        public Converter(Token[] tokens)
        {
            if (Converter.noop == null)
            {
                Converter.noop = new[]
                {
                    "??", "?:", "?", ":"
                };
            }
            this.tokens = tokens.Where(delegate(Token t)
            {
                return !t.CanOmit;
            }).ToArray();
            this.last = new Token("", TokenType.None, 0, 0);
            if (this.tokens.Length > 0)
                this.cur = this.tokens[0];
            else
                this.cur = this.last;
        }

        private void MoveNext()
        {
            if (this.pos < this.tokens.Length)
            {
                this.pos = this.pos + 1;
                if (this.pos < this.tokens.Length)
                    this.cur = this.tokens[this.pos];
                else
                    this.cur = this.last;
            }
            else
                this.cur = this.last;
        }

        public void Convert()
        {
            Debug.WriteLine("// This source code is generated by cs2adm.");
            while (this.cur != this.last)
            {
                switch (this.cur.Text)
                {
                    case "using":
                        while (this.cur.Text != ";")
                            this.MoveNext();
                        this.MoveNext();
                        break;
                    case "namespace":
                        this.ReadNamespace();
                        break;
                    default:
                        throw this.Abort("syntax error");
                }
            }
        }

        private string ReadUsing()
        {
            var sw = new StringWriter();
            this.MoveNext();
            while (this.cur.Text != ";")
            {
                sw.Write(this.cur.Text);
                this.MoveNext();
            }
            this.MoveNext();
            sw.Close();
            return sw.ToString();
        }

        private void ReadNamespace()
        {
            this.MoveNext();
            while (this.cur.Text != "{")
                this.MoveNext();
            this.MoveNext();
            while (this.cur != this.last && this.cur.Text != "}")
                this.ReadNamespaceInternal("private");
            this.MoveNext();
        }

        private void ReadNamespaceInternal(string access)
        {
            if (Converter.IsAccess(this.cur.Text))
            {
                var acc = this.cur.Text;
                this.MoveNext();
                this.ReadNamespaceInternal(acc);
            }
            else if (this.cur.Text == "class")
                this.ReadClass(access);
            else if (this.cur.Text == "enum")
                this.ReadEnum(access);
            else if (this.cur.Text == "abstract")
                this.MoveNext();
            else
                throw this.Abort("not supported");
        }

        private void ReadEnum(string access)
        {
            this.MoveNext();
            var name = this.cur.Text;
            this.MoveNext();
            Debug.WriteLine();
            Debug.WriteLine("class {0}", name);
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            Debug.WriteLine("{{");
            this.MoveNext();
            var v = 0;
            while (this.cur != this.last && this.cur.Text != "}")
            {
                var id = this.cur.Text;
                this.MoveNext();
                if (this.cur.Text == "=")
                {
                    this.MoveNext();
                    v = Int32.Parse(this.cur.Text);
                    this.MoveNext();
                }
                Debug.WriteLine("    const int {0} = {1};", id, v);
                v = v + 1;
                if (this.cur.Text == ",") this.MoveNext();
            }
            this.MoveNext();
            Debug.WriteLine("}}");
        }

        private void ReadClass(string access)
        {
            this.MoveNext();
            var name = this.cur.Text;
            this.MoveNext();
            Debug.WriteLine();
            Debug.Write("class ");
            Debug.Write("{0}", name);
            if (this.cur.Text == ":")
            {
                this.MoveNext();
                Debug.Write(" : {0}", this.cur.Text);
                this.MoveNext();
            }
            Debug.WriteLine();
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            Debug.WriteLine("{{");
            this.MoveNext();
            while (this.cur != this.last && this.cur.Text != "}")
                this.ReadMember("private", false, null);
            this.MoveNext();
            Debug.WriteLine("}}");
        }

        private void ReadMember(string access, bool isStatic, string opt)
        {
            var token = this.cur.Text;
            if (token == "static")
            {
                this.MoveNext();
                this.ReadMember(access, true, opt);
            }
            else if (token == "abstract"
                || token == "virtual"
                || token == "override")
            {
                this.MoveNext();
                this.ReadMember(access, isStatic, token);
            }
            else if (Converter.IsAccess(token))
            {
                this.MoveNext();
                this.ReadMember(token, isStatic, opt);
            }
            else
            {
                var tn = this.ReadDecl(false);
                switch (this.cur.Text)
                {
                    case "(":
                        this.ReadMethod(tn.Name, tn.Type, access, isStatic, opt);
                        break;
                    case ";":
                        this.ReadField(tn.Name, tn.Type, access, isStatic);
                        break;
                    case "{":
                        this.ReadProperty(tn.Name, tn.Type, access, isStatic);
                        break;
                    case "=":
                        throw this.Abort("default value not supported");
                    default:
                        throw this.Abort("syntax error");
                }
            }
        }

        private void ReadProperty(string name, string t, string access, bool isStatic)
        {
            var autoField = false;
            Debug.WriteLine();
            this.MoveNext();
            while (this.cur.Text != "}")
            {
                var acc = access;
                if (Converter.IsAccess(this.cur.Text))
                {
                    acc = this.cur.Text;
                    this.MoveNext();
                }
                if (this.cur.Text == "get" || this.cur.Text == "set")
                {
                    var act = this.cur.Text;
                    this.MoveNext();
                    if (this.cur.Text == ";")
                    {
                        this.MoveNext();
                        if (!autoField)
                        {
                            this.MakeField("_" + name, t, "private", isStatic);
                            autoField = true;
                        }
                    }
                    Debug.Write("    ");
                    if (isStatic) Debug.Write("static ");
                    Debug.Write("function {0}_{1}", act, name);
                    if (act == "get")
                    {
                        Debug.Write(" : {0}", t);
                        if (autoField)
                        {
                            Debug.WriteLine(" {{ return _{0}; }}", name);
                        }
                        else
                        {
                            Debug.WriteLine();
                            this.indent = "    ";
                            this.ReadBlock();
                        }
                    }
                    else
                    {
                        Debug.Write("(value : {0})", t);
                        if (autoField)
                            Debug.WriteLine(" {{ _{0} = value; }}", name);
                        else
                        {
                            Debug.WriteLine();
                            this.indent = "    ";
                            this.ReadBlock();
                        }
                    }
                }
                else
                    throw this.Abort("syntax error");
            }
            this.MoveNext();
        }

        private void ReadField(string name, string t, string access, bool isStatic)
        {
            this.MoveNext();
            this.MakeField(name, t, access, isStatic);
        }

        public static bool IsPrimitive(string t)
        {
            return t == "bool"
                || t == "byte"
                || t == "char"
                || t == "short"
                || t == "ushort"
                || t == "int"
                || t == "uint";
        }

        private void MakeField(string name, string t, string access, bool isStatic)
        {
            Debug.Write("    ");
            if (isStatic) Debug.Write("static ");
            if (IsPrimitive(t))
                Debug.WriteLine("{1} {0};", name, t);
            else
                Debug.WriteLine("var {0} : {1};", name, t);
        }

        private void ReadMethod(string name, string t, string access, bool isStatic, string opt)
        {
            Debug.WriteLine();
            Debug.Write("    ");
            if (isStatic) Debug.Write("static ");
            if (t == null)
            {
                // constructor
                this.MoveNext();
                if (this.cur.Text != ")") throw this.Abort("must be ')'");
                Debug.WriteLine("function ctor");
                this.ReadArgs();
            }
            else
            {
                if (opt == null)
                    Debug.Write("function");
                else if (opt == "abstract")
                    Debug.Write("virtual");
                else
                    Debug.Write(opt);
                Debug.Write(" {0}(", name);
                this.MoveNext();
                this.ReadArgs();
                if (t == "void")
                    Debug.Write(")");
                else
                    Debug.Write(") : {0}", t);
                if (opt == "abstract") Debug.Write(" {{}}");
                Debug.WriteLine();
            }
            if (opt == "abstract")
            {
                if (this.cur.Text != ";") throw this.Abort("must be ';'");
                this.MoveNext();
            }
            else
            {
                if (this.cur.Text != "{") throw this.Abort("block required");
                this.indent = "    ";
                this.ReadBlock();
            }
        }

        private void ReadArgs()
        {
            while (this.cur.Text != ")")
            {
                this.ReadArg();
                if (this.cur.Text == ",")
                {
                    Debug.Write(", ");
                    this.MoveNext();
                }
            }
            this.MoveNext();
        }

        private TypeName ReadDecl(bool arg)
        {
            var list = new List<string>();
            var seps = "(){};=";
            if (arg) seps = seps + ",";
            while (this.cur.Text.Length > 1 || seps.IndexOf(this.cur.Text) < 0)
            {
                list.Add(this.cur.Text);
                this.MoveNext();
            }
            if (list.Count < 1)
                throw this.Abort("missing type or name");
            var last = list.Count - 1;
            var name = list[last];
            list.RemoveAt(last);
            if (list.Count > 0)
            {
                var t = String.Concat(list.ToArray());
                return new TypeName(t, name);
            }
            else
                return new TypeName(null, name);
        }

        private void ReadArg()
        {
            var tn = this.ReadDecl(true);
            if (tn.Type == null)
                throw this.Abort("missing type or name");
            Debug.Write(tn.Name + " : " + tn.Type);
        }

        private void ReadBlockOrSentence()
        {
            if (this.cur.Text == ";")
            {
                this.MoveNext();
                Debug.Write("()");
            }
            else if (this.cur.Text == "{")
                this.ReadBlock();
            else
            {
                var bak = this.indent;
                this.indent = this.indent + "    ";
                this.ReadSentence();
                this.indent = bak;
            }
        }

        private void ReadBlock()
        {
            if (this.cur.Text != "{") throw this.Abort("block required");
            var bak = this.indent;
            Debug.WriteLine("{0}{1}", indent, "{");
            this.indent = this.indent + "    ";
            this.MoveNext();
            while (this.cur != this.last && this.cur.Text != "}")
                this.ReadSentence();
            this.MoveNext();
            this.indent = bak;
            Debug.WriteLine("{0}{1}", indent, "}");
        }

        private void ReadSentence()
        {
            switch (this.cur.Text)
            {
                case "return":
                    Debug.Write("{0}return ", indent);
                    this.MoveNext();
                    this.ReadExpr(false);
                    Debug.WriteLine(";");
                    break;
                case "if":
                    this.ReadIf();
                    break;
                case "while":
                    this.ReadWhile();
                    break;
                case "switch":
                    this.ReadSwitch();
                    break;
                case "for":
                    this.ReadFor();
                    break;
                case "continue":
                case "break":
                    throw this.Abort("not supported");
                case "throw":
                    this.MoveNext();
                    Debug.Write(this.indent);
                    Debug.Write("raise(");
                    this.ReadExpr(false);
                    Debug.WriteLine(");");
                    break;
                case "var":
                    this.ReadVar();
                    break;
                default:
                    Debug.Write(this.indent);
                    this.ReadExpr(false);
                    Debug.WriteLine(";");
                    break;
            }
        }

        private void ReadExpr(bool array)
        {
            var seps = ");:";
            if (array)
            {
                seps = ",}";
                if (seps.IndexOf(this.cur.Text) >= 0)
                    throw this.Abort("element required");
            }
            while (this.cur.Text.Length > 1 || seps.IndexOf(this.cur.Text) < 0)
            {
                var t = this.cur.Text;
                if (t == "(")
                {
                    this.isNew = false;
                    this.MoveNext();
                    Debug.Write("(");
                    this.ReadExpr(false);
                    Debug.Write(")");
                }
                else if (t == ",")
                {
                    this.MoveNext();
                    Debug.Write(", ");
                }
                else if (t == "new")
                {
                    this.MoveNext();
                    if (this.cur.Text == "[")
                        this.ReadArray();
                    else
                    {
                        Debug.Write("new ");
                        this.isNew = true;
                    }
                }
                else if (t == "delegate")
                    this.ReadDelegate();
                else if (t == "int")
                {
                    this.MoveNext();
                    Debug.Write("int ");
                }
                else if (t == "is" || t == "as")
                {
                    this.MoveNext();
                    Debug.Write(" " + t + " ");
                }
                else if (t == "." || this.cur.Type != TokenType.Operator)
                {
                    this.MoveNext();
                    Debug.Write("{0}", t);
                }
                else if (t == "!")
                {
                    this.MoveNext();
                    Debug.Write("not ");
                }
                else if (t == "~")
                {
                    this.MoveNext();
                    Debug.Write("~~~");
                }
                else if (Converter.noop.Contains(t))
                    throw this.Abort("not supported");
                else if (this.isNew || t == "]")
                {
                    this.MoveNext();
                    Debug.Write(t);
                }
                else if (t == "[" || t == "++" || t == "--")
                {
                    this.MoveNext();
                    Debug.Write(t);
                }
                else
                {
                    this.MoveNext();
                    Debug.Write(" " + t + " ");
                }
            }
            if (!array) this.MoveNext();
        }

        private void ReadIf()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            Debug.Write("if (");
            this.ReadIfInternal();
        }

        private void ReadIfInternal()
        {
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine(")");
            this.ReadBlockOrSentence();
            if (this.cur.Text == "else")
            {
                this.MoveNext();
                Debug.Write(this.indent);
                if (this.cur.Text == "if")
                {
                    this.MoveNext();
                    Debug.Write("else if (");
                    this.ReadIfInternal();
                }
                else
                {
                    Debug.WriteLine("else");
                    this.ReadBlockOrSentence();
                }
            }
        }

        private void ReadWhile()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            Debug.Write("while (");
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.Write(")");
            if (this.cur.Text == ";")
            {
                this.MoveNext();
                Debug.WriteLine(";");
            }
            else
            {
                Debug.WriteLine();
                this.ReadBlockOrSentence();
            }
        }

        private void ReadFor()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            Debug.Write("for (");
            this.ReadExpr(false);
            Debug.Write("; ");
            this.ReadExpr(false);
            Debug.Write("; ");
            this.ReadExpr(false);
            Debug.WriteLine(")");
            this.ReadBlockOrSentence();
        }

        private void ReadSwitch()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            Debug.Write("switch (");
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine(")");
            Debug.Write("{0}{{", this.indent);
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            this.MoveNext();
            while (this.cur.Text != "}")
            {
                if (this.cur.Text == "case")
                {
                    while (this.cur.Text == "case")
                    {
                        this.MoveNext();
                        Debug.Write(this.indent);
                        Debug.Write("case ");
                        this.ReadExpr(false);
                        Debug.WriteLine(":");
                    }
                    this.ReadCaseBlock();
                }
                else if (this.cur.Text == "default")
                {
                    this.MoveNext();
                    if (this.cur.Text != ":") throw this.Abort("must be ':'");
                    Debug.Write(this.indent);
                    Debug.WriteLine("default:");
                    this.MoveNext();
                    this.ReadCaseBlock();
                }
                else
                    throw this.Abort("syntax error");
            }
            this.MoveNext();
        }

        private void ReadCaseBlock()
        {
            Debug.WriteLine();
            var bak = this.indent;
            this.indent = this.indent + "    ";
            while (this.cur.Text != "break" && this.cur.Text != "return" && this.cur.Text != "throw")
                this.ReadSentence();
            this.ReadSentence();
            this.indent = bak;
        }

        private void ReadVar()
        {
            this.MoveNext();
            if (this.cur.Type != TokenType.Any) throw this.Abort("name required");
            Debug.Write(this.indent);
            Debug.Write("var {0} = ", this.cur.Text);
            this.MoveNext();
            if (this.cur.Text != "=") throw this.Abort("must be '='");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine(";");
        }

        private void ReadDelegate()
        {
            this.MoveNext();
            if (this.cur.Text != "(") throw this.Abort("argument required");
            this.MoveNext();
            Debug.Write("(fun");
            while (this.cur.Text != ")")
            {
                var tn = this.ReadDecl(true);
                Debug.Write(" ({0} : {1})", tn.Name, tn.Type);
                if (this.cur.Text == ",") this.MoveNext();
            }
            this.MoveNext();
            Debug.WriteLine(" ->");
            this.ReadBlock();
            Debug.Write(this.indent);
            Debug.Write(")");
        }

        private void ReadArray()
        {
            this.MoveNext();
            if (this.cur.Text != "]") throw this.Abort("must be ']'");
            this.MoveNext();
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            this.MoveNext();
            Debug.Write("[| ");
            while (this.cur.Text != "}")
            {
                this.ReadExpr(true);
                if (this.cur.Text == ",")
                {
                    this.MoveNext();
                    if (this.cur.Text != "}") Debug.Write("; ");
                }
            }
            this.MoveNext();
            Debug.Write(" |]");
        }

        private Exception Abort(string message)
        {
            return new Exception(String.Format(
                "[{0}, {1}] {2}: {3}", this.cur.Line, this.cur.Column, message, this.cur.Text));
        }

        public static bool IsAccess(string s)
        {
            return s == "public" || s == "protected" || s == "private";
        }
    }
}
