namespace Fable.AST.Fable
open Fable
open Fable.AST

(** ##Decorators *)
type Decorator =
    | Decorator of fullName: string * args: obj list
    member x.FullName = match x with Decorator (prop,_) -> prop
    member x.Arguments = match x with Decorator (_,prop) -> prop
    member x.Name = x.FullName.Substring (x.FullName.LastIndexOf '.' + 1)

(** ##Types *)
type Type =
    | Any
    | Unit
    | Boolean
    | String
    | Number of NumberKind
    | Option of genericArg: Type
    | Array of genericArg: Type
    | Tuple of genericArgs: Type list
    | Function of argTypes: Type list * returnType: Type
    | GenericParam of name: string
    | Enum of fullName: string
    | DeclaredType of Entity * genericArgs: Type list
    member x.FullName =
        match x with
        | Number numberKind -> sprintf "%A" x
        | Enum fullName -> fullName
        | Array typ -> typ.FullName + "[]"
        | Function (argTypes, returnType) ->
            "(" + (argTypes |> Seq.map (fun x -> x.FullName) |> String.concat ", ") + ")=>" + returnType.FullName
        | DeclaredType(ent,_) -> ent.FullName
        | _ -> sprintf "%A" x
    member x.GenericArgs =
        match x with
        | Array genArg -> [genArg]
        | Tuple genArgs -> genArgs
        | Function(argTypes, returnType) -> argTypes@[returnType]
        | DeclaredType(_, genArgs) -> genArgs
        | _ -> []

and TypeKind =
    // | Boolean | String | Number _ | Enum _
    // | Function _ | DeclaredType _
    | Other = 0
    | Any = 1
    | Unit = 2
    | Option = 3
    | Array = 4
    | Tuple = 5
    | GenericParam = 6
    | Interface = 7

(** ##Entities *)
and EntityKind =
    | Module
    | Union of cases: Map<string, Type list>
    | Record of fields: (string*Type) list
    | Exception of fields: (string*Type) list
    | Class of baseClass: (string*Expr) option * properties: (string*Type) list
    | Interface

and Entity(kind: Lazy<_>, file, fullName, members: Lazy<Member list>,
           ?genParams, ?interfaces, ?decorators, ?isPublic) =
    let genParams = defaultArg genParams []
    let decorators = defaultArg decorators []
    let interfaces = defaultArg interfaces []
    let isPublic = defaultArg isPublic true
    member x.Kind: EntityKind = kind.Value
    member x.File: string option = file
    member x.FullName: string = fullName
    member x.GenericParameters: string list = genParams
    // member x.Members: Member list = members
    member x.Interfaces: string list = interfaces
    member x.Decorators: Decorator list = decorators
    member x.IsPublic: bool = isPublic
    member x.Name =
        x.FullName.Substring(x.FullName.LastIndexOf('.') + 1)
    member x.Namespace =
        let fullName = x.FullName
        match fullName.LastIndexOf "." with
        | -1 -> ""
        | 0 -> failwithf "Unexpected entity full name: %s" fullName
        | _ as i -> fullName.Substring(0, i)
    member x.HasInterface (fullName: string) =
        List.exists ((=) fullName) interfaces
    member x.TryGetDecorator decorator =
        decorators |> List.tryFind (fun x -> x.Name = decorator)
    member x.TryGetMember(name, kind, isStatic, argTypes, ?argsEqual) =
        let argsEqual = defaultArg argsEqual (=)
        members.Value |> List.tryFind (fun m ->
            if m.IsStatic <> isStatic
                || m.Name <> name
                || m.Kind <> kind
            then false
            elif m.OverloadIndex.IsNone
            then true
            else argsEqual m.ArgumentTypes argTypes)
    static member CreateRootModule fileName modFullName =
        Entity (lazy Module, Some fileName, modFullName, lazy [], [], [], [], true)
    override x.ToString() = sprintf "%s %A" x.Name kind

and Declaration =
    | ActionDeclaration of Expr * SourceLocation
    /// Module members are also declared as variables, so they need
    /// a private name that doesn't conflict with enclosing scope (see #130)
    | EntityDeclaration of Entity * privateName: string * Declaration list * SourceLocation
    | MemberDeclaration of Member * privateName: string option * args: Ident list * body: Expr * SourceLocation
    member x.Range =
        match x with
        | ActionDeclaration (_,r) -> r
        | EntityDeclaration (_,_,_,r) -> r
        | MemberDeclaration (_,_,_,_,r) -> r

and MemberKind =
    | Constructor
    | Method
    | Getter
    | Setter
    | Field

and Member(name, kind, argTypes, returnType, ?originalType, ?genParams, ?decorators,
           ?isPublic, ?isMutable, ?isStatic, ?isSymbol, ?hasRestParams, ?overloadIndex) =
    member x.Name: string = name
    member x.Kind: MemberKind = kind
    member x.ArgumentTypes: Type list = argTypes
    member x.ReturnType: Type = returnType
    member x.OriginalCurriedType: Type = defaultArg originalType (Function(argTypes, returnType))
    member x.GenericParameters: string list = defaultArg genParams []
    member x.Decorators: Decorator list = defaultArg decorators []
    member x.IsPublic: bool = defaultArg isPublic true
    member x.IsMutable: bool = defaultArg isMutable false
    member x.IsStatic: bool = defaultArg isStatic false
    member x.IsSymbol: bool = defaultArg isSymbol false
    member x.HasRestParams: bool = defaultArg hasRestParams false
    member x.OverloadIndex: int option = overloadIndex
    member x.OverloadName: string =
        match overloadIndex with
        | Some i -> name + "_" + (string i)
        | None -> name
    member x.TryGetDecorator decorator =
        x.Decorators |> List.tryFind (fun x -> x.Name = decorator)
    override x.ToString() = sprintf "%A %s" kind name

and ExternalEntity =
    | ImportModule of fullName: string * moduleName: string * isNs: bool
    | GlobalModule of fullName: string
    member x.FullName =
        match x with ImportModule (fullName, _, _)
                   | GlobalModule fullName -> fullName

and File(fileName, root, decls, ?usedVarNames) =
    member x.FileName: string = fileName
    member x.Root: Entity = root
    member x.Declarations: Declaration list = decls
    member x.UsedVarNames: Set<string> = defaultArg usedVarNames Set.empty
    member x.Range =
        match decls with
        | [] -> SourceLocation.Empty
        | decls -> SourceLocation.Empty + (List.last decls).Range

and Project(name, baseDir, fileMap, ?assemblyFile, ?importPath) =
    member __.Name: string = name
    member __.BaseDir: string = baseDir
    member __.FileMap: Map<string, string> = fileMap
    member __.AssemblyFileName: string option = assemblyFile
    member __.ImportPath: string option = importPath

(** ##Expressions *)
and ApplyInfo = {
        ownerType: Type
        ownerFullName: string
        methodName: string
        methodKind: MemberKind
        callee: Expr option
        args: Expr list
        returnType: Type
        range: SourceLocation option
        fileName: string
        decorators: Decorator list
        calleeTypeArgs: Type list
        methodTypeArgs: Type list
        /// If the method accepts a lambda as first argument, indicates its arity 
        lambdaArgArity: int
    }

and ApplyKind =
    | ApplyMeth | ApplyGet | ApplyCons

and ArrayConsKind =
    | ArrayValues of Expr list
    | ArrayAlloc of Expr

and Ident =
    { name: string; typ: Type }
    static member getType (i: Ident) = i.typ

and ValueKind =
    | Null
    | This
    | Super
    | Spread of Expr
    | TypeRef of Entity
    | IdentValue of Ident
    | ImportRef of memb: string * path: string
    | NumberConst of U2<int,float> * NumberKind
    | StringConst of string
    | BoolConst of bool
    | RegexConst of source: string * flags: RegexFlag list
    | ArrayConst of ArrayConsKind * Type
    | TupleConst of Expr list
    | UnaryOp of UnaryOperator
    | BinaryOp of BinaryOperator
    | LogicalOp of LogicalOperator
    | Lambda of args: Ident list * body: Expr
    | Emit of string
    member x.Type =
        match x with
        | Null -> Any
        | Spread x -> x.Type
        | IdentValue {typ=typ} -> typ
        | This | Super | ImportRef _ | TypeRef _ | Emit _ -> Any
        | NumberConst (_,kind) -> Number kind
        | StringConst _ -> String
        | RegexConst _ ->
            let fullName = "System.Text.RegularExpressions.Regex"
            DeclaredType(Entity(lazy Class(None, []), None, fullName, lazy []), [])        
        | BoolConst _ -> Boolean
        | ArrayConst (_, typ) -> Array typ
        | TupleConst exprs -> List.map Expr.getType exprs |> Tuple
        | UnaryOp _ -> Function([Any], Any)
        | BinaryOp _ | LogicalOp _ -> Function([Any; Any], Any)
        | Lambda (args, body) -> Function(List.map Ident.getType args, body.Type)
    member x.Range: SourceLocation option =
        match x with
        | Lambda (_, body) -> body.Range
        | _ -> None

and LoopKind =
    | While of guard: Expr * body: Expr
    | For of ident: Ident * start: Expr * limit: Expr * body: Expr * isUp: bool
    | ForOf of ident: Ident * enumerable: Expr * body: Expr

and Expr =
    // Pure Expressions
    | Value of value: ValueKind
    | ObjExpr of decls: Declaration list * interfaces: string list * baseClass: Expr option * range: SourceLocation option
    | IfThenElse of guardExpr: Expr * thenExpr: Expr * elseExpr: Expr * range: SourceLocation option
    | Apply of callee: Expr * args: Expr list * kind: ApplyKind * typ: Type * range: SourceLocation option
    | Quote of Expr

    // Pseudo-Statements
    | Throw of Expr * typ: Type * range: SourceLocation option
    | DebugBreak of range: SourceLocation option
    | Loop of LoopKind * range: SourceLocation option
    | VarDeclaration of var: Ident * value: Expr * isMutable: bool
    | Set of callee: Expr * property: Expr option * value: Expr * range: SourceLocation option
    | Sequential of Expr list * range: SourceLocation option
    | TryCatch of body: Expr * catch: (Ident * Expr) option * finalizer: Expr option * range: SourceLocation option
    | Switch of matchValue: Expr * cases: (Expr list * Expr) list * defaultCase: Expr option * typ: Type * range: SourceLocation option

    // This wraps expressions with a different type for compile-time checkings
    // E.g. enums, ignored expressions so they don't trigger a return in functions
    | Wrapped of Expr * Type

    // Label declaration (can be placed before a loop) with break/continue
    // that optionally take the name of the loop to break/continue from;
    // (Those, including 'return' do not appear in F# code but may be generated by a plugin)
    | Label of Ident * Expr * SourceLocation option
    | Break of Ident option * SourceLocation option
    | Continue of Ident option * SourceLocation option
    | Return of Expr * SourceLocation option

    static member getType (expr: Expr) = expr.Type

    member x.Type =
        match x with
        | Value kind -> kind.Type
        | ObjExpr _ -> Any
        | Wrapped (_,typ) | Apply (_,_,_,typ,_) | Throw (_,typ,_) -> typ
        | IfThenElse (_,thenExpr,elseExpr,_) -> thenExpr.Type
        | DebugBreak _ | Loop _ | Set _ | VarDeclaration _ -> Unit
        | Sequential (exprs,_) ->
            match exprs with
            | [] -> Unit
            | exprs -> (Seq.last exprs).Type
        | TryCatch (body,_,_,_)
        | Label(_, body, _)-> body.Type
        | Switch (_,_,_,t,_) -> t
        | Break _
        | Continue _
        | Return _ -> Unit
        // TODO: Quotations must have their own primitive? type
        | Quote _ -> Any

    member x.Range: SourceLocation option =
        match x with
        | Value v -> v.Range
        | VarDeclaration (_,e,_) | Wrapped (e,_) | Quote e -> e.Range
        | ObjExpr (_,_,_,range)
        | Apply (_,_,_,_,range)
        | IfThenElse (_,_,_,range)
        | Throw (_,_,range)
        | DebugBreak range
        | Loop (_,range)
        | Set (_,_,_,range)
        | Sequential (_,range)
        | TryCatch (_,_,_,range)
        | Switch (_,_,_,_,range)
        | Label (_, _, range)
        | Break (_, range)
        | Continue (_, range) 
        | Return (_, range) -> range
