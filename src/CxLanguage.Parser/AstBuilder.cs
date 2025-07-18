using CxLanguage.Core.Ast;
using CxLanguage.Core.Types;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using static CxParser;

namespace CxLanguage.Parser;

/// <summary>
/// Converts ANTLR parse tree to Cx AST
/// </summary>
public class AstBuilder : CxBaseVisitor<AstNode>
{
    private readonly string? _fileName;

    public AstBuilder(string? fileName = null)
    {
        _fileName = fileName;
    }

    public AstNode BuildAst(IParseTree tree)
    {
        return Visit(tree);
    }

    public override AstNode VisitProgram(ProgramContext context)
    {
        var program = new ProgramNode();
        SetLocation(program, context);

        foreach (var stmtContext in context.statement())
        {
            var statement = (StatementNode)Visit(stmtContext);
            if (statement != null)
            {
                program.Statements.Add(statement);
            }
        }

        return program;
    }

    public override AstNode VisitStatement(StatementContext context)
    {
        // Get the first child of the statement (the actual statement type)
        if (context.ChildCount > 0)
        {
            var child = context.GetChild(0);
            var result = Visit(child);
            return result;
        }
        
        throw new InvalidOperationException("Statement context has no children");
    }

    public override AstNode VisitVariableDeclaration(VariableDeclarationContext context)
    {
        var varDecl = new VariableDeclarationNode();
        SetLocation(varDecl, context);

        varDecl.Name = context.IDENTIFIER().GetText();
        varDecl.Initializer = (ExpressionNode)Visit(context.expression());

        return varDecl;
    }

    public override AstNode VisitFunctionDeclaration(FunctionDeclarationContext context)
    {
        var funcDecl = new FunctionDeclarationNode();
        SetLocation(funcDecl, context);

        funcDecl.Name = context.IDENTIFIER().GetText();
        funcDecl.IsAsync = context.GetText().StartsWith("async");
        
        // Store source position information for self keyword
        funcDecl.StartLine = context.Start.Line;
        funcDecl.EndLine = context.Stop.Line;
        
        // Parse access modifier if present
        if (context.accessModifier() != null)
        {
            funcDecl.AccessModifier = ParseAccessModifier(context.accessModifier());
        }
        
        // Parameters
        if (context.parameterList() != null)
        {
            foreach (var paramContext in context.parameterList().parameter())
            {
                var param = new ParameterNode
                {
                    Name = paramContext.IDENTIFIER().GetText(),
                    Type = paramContext.type() != null ? ParseType(paramContext.type()) : CxType.Any
                };
                SetLocation(param, paramContext);
                funcDecl.Parameters.Add(param);
            }
        }

        // Return type
        if (context.type() != null)
        {
            funcDecl.ReturnType = ParseType(context.type());
        }

        // Body
        funcDecl.Body = (BlockStatementNode)Visit(context.blockStatement());

        return funcDecl;
    }

    public override AstNode VisitImportStatement(ImportStatementContext context)
    {
        var importStmt = new ImportStatementNode();
        SetLocation(importStmt, context);

        importStmt.Alias = context.IDENTIFIER().GetText();
        importStmt.ModulePath = context.STRING_LITERAL().GetText().Trim('"');

        return importStmt;
    }

    public override AstNode VisitBlockStatement(BlockStatementContext context)
    {
        var block = new BlockStatementNode();
        SetLocation(block, context);

        foreach (var stmtContext in context.statement())
        {
            var statement = (StatementNode)Visit(stmtContext);
            block.Statements.Add(statement);
        }

        return block;
    }

    public override AstNode VisitExpressionStatement(ExpressionStatementContext context)
    {
        var exprStmt = new ExpressionStatementNode();
        SetLocation(exprStmt, context);

        exprStmt.Expression = (ExpressionNode)Visit(context.expression());

        return exprStmt;
    }

    public override AstNode VisitReturnStatement(ReturnStatementContext context)
    {
        var returnStmt = new ReturnStatementNode();
        SetLocation(returnStmt, context);

        if (context.expression() != null)
        {
            returnStmt.Value = (ExpressionNode)Visit(context.expression());
        }

        return returnStmt;
    }

    public override AstNode VisitIfStatement(IfStatementContext context)
    {
        var ifStmt = new IfStatementNode();
        SetLocation(ifStmt, context);

        ifStmt.Condition = (ExpressionNode)Visit(context.expression());
        ifStmt.ThenStatement = (StatementNode)Visit(context.statement(0));
        
        if (context.statement().Length > 1)
        {
            ifStmt.ElseStatement = (StatementNode)Visit(context.statement(1));
        }

        return ifStmt;
    }

    public override AstNode VisitWhileStatement(WhileStatementContext context)
    {
        var whileStmt = new WhileStatementNode();
        SetLocation(whileStmt, context);

        whileStmt.Condition = (ExpressionNode)Visit(context.expression());
        whileStmt.Body = (StatementNode)Visit(context.statement());

        return whileStmt;
    }

    public override AstNode VisitForStatement(ForStatementContext context)
    {
        var forStmt = new ForStatementNode();
        SetLocation(forStmt, context);

        forStmt.Variable = context.IDENTIFIER().GetText();
        forStmt.Iterable = (ExpressionNode)Visit(context.expression());
        forStmt.Body = (StatementNode)Visit(context.statement());

        return forStmt;
    }

    // Expression visitors
    public override AstNode VisitPrimaryExpression(PrimaryExpressionContext context)
    {
        return Visit(context.primary());
    }

    public override AstNode VisitPrimary(PrimaryContext context)
    {
        if (context.IDENTIFIER() != null)
        {
            var identifier = new IdentifierNode();
            SetLocation(identifier, context);
            identifier.Name = context.IDENTIFIER().GetText();
            return identifier;
        }
        else if (context.STRING_LITERAL() != null)
        {
            var literal = new LiteralNode();
            SetLocation(literal, context);
            literal.Value = context.STRING_LITERAL().GetText().Trim('"');
            literal.Type = LiteralType.String;
            return literal;
        }
        else if (context.NUMBER_LITERAL() != null)
        {
            var literal = new LiteralNode();
            SetLocation(literal, context);
            var text = context.NUMBER_LITERAL().GetText();
            
            if (text.Contains('.'))
            {
                literal.Value = double.Parse(text);
            }
            else
            {
                literal.Value = int.Parse(text);
            }
            literal.Type = LiteralType.Number;
            return literal;
        }
        else if (context.BOOLEAN_LITERAL() != null)
        {
            var literal = new LiteralNode();
            SetLocation(literal, context);
            literal.Value = bool.Parse(context.BOOLEAN_LITERAL().GetText());
            literal.Type = LiteralType.Boolean;
            return literal;
        }
        else if (context.NULL() != null)
        {
            var literal = new LiteralNode();
            SetLocation(literal, context);
            literal.Value = null;
            literal.Type = LiteralType.Null;
            return literal;
        }
        else if (context.SELF() != null)
        {
            var selfRef = new SelfReferenceNode();
            SetLocation(selfRef, context);
            return selfRef;
        }
        else if (context.expression() != null)
        {
            return Visit(context.expression());
        }

        throw new InvalidOperationException($"Unknown primary expression: {context.GetText()}");
    }

    public override AstNode VisitMemberAccess(MemberAccessContext context)
    {
        var memberAccess = new MemberAccessNode();
        SetLocation(memberAccess, context);

        memberAccess.Object = (ExpressionNode)Visit(context.expression());
        memberAccess.Property = context.IDENTIFIER().GetText();

        return memberAccess;
    }

    public override AstNode VisitFunctionCall(FunctionCallContext context)
    {
        var funcCall = new CallExpressionNode();
        SetLocation(funcCall, context);

        funcCall.Callee = (ExpressionNode)Visit(context.expression());

        if (context.argumentList() != null)
        {
            foreach (var argContext in context.argumentList().expression())
            {
                var argument = (ExpressionNode)Visit(argContext);
                funcCall.Arguments.Add(argument);
            }
        }

        return funcCall;
    }

    public override AstNode VisitIndexAccess(IndexAccessContext context)
    {
        var indexAccess = new IndexAccessNode();
        SetLocation(indexAccess, context);

        indexAccess.Object = (ExpressionNode)Visit(context.expression(0));
        indexAccess.Index = (ExpressionNode)Visit(context.expression(1));

        return indexAccess;
    }

    public override AstNode VisitAwaitExpression(AwaitExpressionContext context)
    {
        var awaitExpr = new AwaitExpressionNode();
        SetLocation(awaitExpr, context);
        
        awaitExpr.Expression = (ExpressionNode)Visit(context.expression());
        
        return awaitExpr;
    }

    public override AstNode VisitParallelExpression(ParallelExpressionContext context)
    {
        var parallelExpr = new ParallelExpressionNode();
        SetLocation(parallelExpr, context);
        
        parallelExpr.Expression = (ExpressionNode)Visit(context.expression());
        
        return parallelExpr;
    }

    public override AstNode VisitAdditiveExpression(AdditiveExpressionContext context)
    {
        var binaryExpr = new BinaryExpressionNode();
        SetLocation(binaryExpr, context);

        binaryExpr.Left = (ExpressionNode)Visit(context.expression(0));
        binaryExpr.Right = (ExpressionNode)Visit(context.expression(1));
        
        var operatorText = context.children[1].GetText();
        binaryExpr.Operator = operatorText switch
        {
            "+" => BinaryOperator.Add,
            "-" => BinaryOperator.Subtract,
            _ => throw new InvalidOperationException($"Unknown additive operator: {operatorText}")
        };

        return binaryExpr;
    }

    public override AstNode VisitMultiplicativeExpression(MultiplicativeExpressionContext context)
    {
        var binaryExpr = new BinaryExpressionNode();
        SetLocation(binaryExpr, context);

        binaryExpr.Left = (ExpressionNode)Visit(context.expression(0));
        binaryExpr.Right = (ExpressionNode)Visit(context.expression(1));
        
        var operatorText = context.children[1].GetText();
        binaryExpr.Operator = operatorText switch
        {
            "*" => BinaryOperator.Multiply,
            "/" => BinaryOperator.Divide,
            "%" => BinaryOperator.Modulo,
            _ => throw new InvalidOperationException($"Unknown multiplicative operator: {operatorText}")
        };

        return binaryExpr;
    }

    public override AstNode VisitRelationalExpression(RelationalExpressionContext context)
    {
        var binaryExpr = new BinaryExpressionNode();
        SetLocation(binaryExpr, context);

        binaryExpr.Left = (ExpressionNode)Visit(context.expression(0));
        binaryExpr.Right = (ExpressionNode)Visit(context.expression(1));
        
        var operatorText = context.children[1].GetText();
        binaryExpr.Operator = operatorText switch
        {
            "<" => BinaryOperator.LessThan,
            ">" => BinaryOperator.GreaterThan,
            "<=" => BinaryOperator.LessThanOrEqual,
            ">=" => BinaryOperator.GreaterThanOrEqual,
            "==" => BinaryOperator.Equal,
            "!=" => BinaryOperator.NotEqual,
            _ => throw new InvalidOperationException($"Unknown relational operator: {operatorText}")
        };

        return binaryExpr;
    }

    public override AstNode VisitLogicalExpression(LogicalExpressionContext context)
    {
        var binaryExpr = new BinaryExpressionNode();
        SetLocation(binaryExpr, context);

        binaryExpr.Left = (ExpressionNode)Visit(context.expression(0));
        binaryExpr.Right = (ExpressionNode)Visit(context.expression(1));
        
        var operatorText = context.children[1].GetText();
        binaryExpr.Operator = operatorText switch
        {
            "&&" => BinaryOperator.And,
            "||" => BinaryOperator.Or,
            _ => throw new InvalidOperationException($"Unknown logical operator: {operatorText}")
        };

        return binaryExpr;
    }

    public override AstNode VisitAssignmentExpression(AssignmentExpressionContext context)
    {
        var assignment = new AssignmentExpressionNode();
        SetLocation(assignment, context);

        assignment.Left = (ExpressionNode)Visit(context.expression(0));
        assignment.Right = (ExpressionNode)Visit(context.expression(1));

        // Determine the assignment operator
        var operatorText = context.children[1].GetText(); // The operator is always the second child
        assignment.Operator = operatorText switch
        {
            "=" => AssignmentOperator.Assign,
            "+=" => AssignmentOperator.AddAssign,
            "-=" => AssignmentOperator.SubtractAssign,
            "*=" => AssignmentOperator.MultiplyAssign,
            "/=" => AssignmentOperator.DivideAssign,
            _ => AssignmentOperator.Assign
        };

        return assignment;
    }

    public override AstNode VisitObjectLiteral(ObjectLiteralContext context)
    {
        var objectLiteral = new ObjectLiteralNode();
        SetLocation(objectLiteral, context);
        
        // Parse object properties if present
        if (context.objectPropertyList() != null)
        {
            var propListContext = context.objectPropertyList();
            foreach (var propContext in propListContext.objectProperty())
            {
                var property = new ObjectPropertyNode();
                SetLocation(property, propContext);
                
                // Get the key (identifier or string literal)
                if (propContext.IDENTIFIER() != null)
                {
                    property.Key = propContext.IDENTIFIER().GetText();
                }
                else if (propContext.STRING_LITERAL() != null)
                {
                    // Remove quotes from string literal
                    var stringLiteral = propContext.STRING_LITERAL().GetText();
                    property.Key = stringLiteral.Substring(1, stringLiteral.Length - 2);
                }
                
                // Get the value expression
                property.Value = (ExpressionNode)Visit(propContext.expression());
                
                objectLiteral.Properties.Add(property);
            }
        }
        
        return objectLiteral;
    }

    public override AstNode VisitArrayLiteral(ArrayLiteralContext context)
    {
        var arrayLiteral = new ArrayLiteralNode();
        SetLocation(arrayLiteral, context);
        
        // Parse argument list if present
        if (context.argumentList() != null)
        {
            var argListContext = context.argumentList();
            foreach (var exprContext in argListContext.expression())
            {
                arrayLiteral.Elements.Add((ExpressionNode)Visit(exprContext));
            }
        }
        
        return arrayLiteral;
    }

    public override AstNode VisitUnaryExpression(UnaryExpressionContext context)
    {
        var unaryExpr = new UnaryExpressionNode();
        SetLocation(unaryExpr, context);

        unaryExpr.Operand = (ExpressionNode)Visit(context.expression());
        
        var operatorText = context.children[0].GetText();
        unaryExpr.Operator = operatorText switch
        {
            "!" => UnaryOperator.Not,
            "-" => UnaryOperator.Minus,
            "+" => UnaryOperator.Plus,
            _ => throw new InvalidOperationException($"Unknown unary operator: {operatorText}")
        };

        return unaryExpr;
    }

    private CxType ParseType(TypeContext context)
    {
        var typeText = context.GetText();
        return typeText switch
        {
            "string" => CxType.String,
            "number" => CxType.Number,
            "boolean" => CxType.Boolean,
            "any" => CxType.Any,
            "object" => CxType.Object,
            _ when typeText.StartsWith("array<") => CxType.Object, // Arrays as objects for now
            _ => CxType.Any // Fallback for unknown types
        };
    }

    private void SetLocation(AstNode node, IParseTree? context)
    {
        if (context is ParserRuleContext ruleContext && _fileName != null)
        {
            node.Line = ruleContext.Start.Line;
            node.Column = ruleContext.Start.Column;
            node.SourceFile = _fileName;
        }
    }

    public override AstNode VisitTryStatement(TryStatementContext context)
    {
        var tryStmt = new TryStatementNode();
        SetLocation(tryStmt, context);

        tryStmt.TryBlock = (StatementNode)Visit(context.blockStatement(0));

        if (context.blockStatement().Length > 1)
        {
            tryStmt.CatchVariableName = context.IDENTIFIER()?.GetText();
            tryStmt.CatchBlock = (StatementNode)Visit(context.blockStatement(1));
        }

        return tryStmt;
    }

    public override AstNode VisitThrowStatement(ThrowStatementContext context)
    {
        var throwStmt = new ThrowStatementNode();
        SetLocation(throwStmt, context);

        throwStmt.Expression = (ExpressionNode)Visit(context.expression());

        return throwStmt;
    }

    public override AstNode VisitNewExpression(NewExpressionContext context)
    {
        var newExpr = new NewExpressionNode();
        SetLocation(newExpr, context);

        newExpr.TypeName = context.IDENTIFIER().GetText();

        if (context.argumentList() != null)
        {
            foreach (var argContext in context.argumentList().expression())
            {
                newExpr.Arguments.Add((ExpressionNode)Visit(argContext));
            }
        }

        return newExpr;
    }

    // Class system visitors
    public override AstNode VisitClassDeclaration(ClassDeclarationContext context)
    {
        var classDecl = new ClassDeclarationNode();
        SetLocation(classDecl, context);

        classDecl.Name = context.IDENTIFIER(0).GetText();
        
        // Parse access modifier if present
        if (context.accessModifier() != null)
        {
            classDecl.AccessModifier = ParseAccessModifier(context.accessModifier());
        }

        // Parse base class if present
        if (context.IDENTIFIER().Length > 1)
        {
            classDecl.BaseClass = context.IDENTIFIER(1).GetText();
        }

        // Parse interfaces if present
        if (context.interfaceList() != null)
        {
            foreach (var interfaceContext in context.interfaceList().IDENTIFIER())
            {
                classDecl.Interfaces.Add(interfaceContext.GetText());
            }
        }

        // Parse class body
        if (context.classBody() != null)
        {
            foreach (var memberContext in context.classBody().classMember())
            {
                if (memberContext.fieldDeclaration() != null)
                {
                    classDecl.Fields.Add((FieldDeclarationNode)Visit(memberContext.fieldDeclaration()));
                }
                else if (memberContext.methodDeclaration() != null)
                {
                    classDecl.Methods.Add((MethodDeclarationNode)Visit(memberContext.methodDeclaration()));
                }
                else if (memberContext.constructorDeclaration() != null)
                {
                    classDecl.Constructors.Add((ConstructorDeclarationNode)Visit(memberContext.constructorDeclaration()));
                }
            }
        }

        return classDecl;
    }

    public override AstNode VisitFieldDeclaration(FieldDeclarationContext context)
    {
        var fieldDecl = new FieldDeclarationNode();
        SetLocation(fieldDecl, context);

        fieldDecl.Name = context.IDENTIFIER().GetText();
        fieldDecl.Type = ParseType(context.type());
        
        // Parse access modifier if present
        if (context.accessModifier() != null)
        {
            fieldDecl.AccessModifier = ParseAccessModifier(context.accessModifier());
        }

        // Parse initializer if present
        if (context.expression() != null)
        {
            fieldDecl.Initializer = (ExpressionNode)Visit(context.expression());
        }

        return fieldDecl;
    }

    public override AstNode VisitMethodDeclaration(MethodDeclarationContext context)
    {
        var methodDecl = new MethodDeclarationNode();
        SetLocation(methodDecl, context);

        methodDecl.Name = context.IDENTIFIER().GetText();
        methodDecl.IsAsync = context.GetText().Contains("async");
        
        // Parse access modifier if present
        if (context.accessModifier() != null)
        {
            methodDecl.AccessModifier = ParseAccessModifier(context.accessModifier());
        }

        // Parameters
        if (context.parameterList() != null)
        {
            foreach (var paramContext in context.parameterList().parameter())
            {
                var param = new ParameterNode
                {
                    Name = paramContext.IDENTIFIER().GetText(),
                    Type = paramContext.type() != null ? ParseType(paramContext.type()) : CxType.Any
                };
                SetLocation(param, paramContext);
                methodDecl.Parameters.Add(param);
            }
        }

        // Return type
        if (context.type() != null)
        {
            methodDecl.ReturnType = ParseType(context.type());
        }

        // Body
        methodDecl.Body = (BlockStatementNode)Visit(context.blockStatement());

        return methodDecl;
    }

    public override AstNode VisitConstructorDeclaration(ConstructorDeclarationContext context)
    {
        var ctorDecl = new ConstructorDeclarationNode();
        SetLocation(ctorDecl, context);

        // Parse access modifier if present
        if (context.accessModifier() != null)
        {
            ctorDecl.AccessModifier = ParseAccessModifier(context.accessModifier());
        }

        // Parameters
        if (context.parameterList() != null)
        {
            foreach (var paramContext in context.parameterList().parameter())
            {
                var param = new ParameterNode
                {
                    Name = paramContext.IDENTIFIER().GetText(),
                    Type = paramContext.type() != null ? ParseType(paramContext.type()) : CxType.Any
                };
                SetLocation(param, paramContext);
                ctorDecl.Parameters.Add(param);
            }
        }

        // Body
        ctorDecl.Body = (BlockStatementNode)Visit(context.blockStatement());

        return ctorDecl;
    }

    public override AstNode VisitInterfaceDeclaration(InterfaceDeclarationContext context)
    {
        var interfaceDecl = new InterfaceDeclarationNode();
        SetLocation(interfaceDecl, context);

        interfaceDecl.Name = context.IDENTIFIER().GetText();
        
        // Parse access modifier if present
        if (context.accessModifier() != null)
        {
            interfaceDecl.AccessModifier = ParseAccessModifier(context.accessModifier());
        }

        // Parse extended interfaces if present
        if (context.interfaceList() != null)
        {
            foreach (var interfaceContext in context.interfaceList().IDENTIFIER())
            {
                interfaceDecl.ExtendedInterfaces.Add(interfaceContext.GetText());
            }
        }

        // Parse interface body
        if (context.interfaceBody() != null)
        {
            foreach (var memberContext in context.interfaceBody().interfaceMember())
            {
                if (memberContext.interfaceMethodSignature() != null)
                {
                    interfaceDecl.Methods.Add((InterfaceMethodSignatureNode)Visit(memberContext.interfaceMethodSignature()));
                }
                else if (memberContext.interfacePropertySignature() != null)
                {
                    interfaceDecl.Properties.Add((InterfacePropertySignatureNode)Visit(memberContext.interfacePropertySignature()));
                }
            }
        }

        return interfaceDecl;
    }

    public override AstNode VisitInterfaceMethodSignature(InterfaceMethodSignatureContext context)
    {
        var methodSig = new InterfaceMethodSignatureNode();
        SetLocation(methodSig, context);

        methodSig.Name = context.IDENTIFIER().GetText();

        // Parameters
        if (context.parameterList() != null)
        {
            foreach (var paramContext in context.parameterList().parameter())
            {
                var param = new ParameterNode
                {
                    Name = paramContext.IDENTIFIER().GetText(),
                    Type = paramContext.type() != null ? ParseType(paramContext.type()) : CxType.Any
                };
                SetLocation(param, paramContext);
                methodSig.Parameters.Add(param);
            }
        }

        // Return type
        if (context.type() != null)
        {
            methodSig.ReturnType = ParseType(context.type());
        }

        return methodSig;
    }

    public override AstNode VisitInterfacePropertySignature(InterfacePropertySignatureContext context)
    {
        var propSig = new InterfacePropertySignatureNode();
        SetLocation(propSig, context);

        propSig.Name = context.IDENTIFIER().GetText();
        propSig.Type = ParseType(context.type());

        return propSig;
    }

    private AccessModifier ParseAccessModifier(AccessModifierContext context)
    {
        var text = context.GetText();
        return text switch
        {
            "public" => AccessModifier.Public,
            "private" => AccessModifier.Private,
            "protected" => AccessModifier.Protected,
            _ => AccessModifier.Public
        };
    }
}
