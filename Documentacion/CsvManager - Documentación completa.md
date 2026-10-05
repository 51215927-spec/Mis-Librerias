# CsvManager — Documentación completa

Guía para usar, entender y mantener `CsvManager`, la clase que guarda objetos de C# en archivos CSV y los reconstruye al leerlos.

Está escrita para que la pueda leer cualquier persona, incluso sin experiencia previa con Reflection ni con archivos CSV. Cada sección es independiente: se puede saltar directamente a la que interese.

> **Estado de verificación:** este código y esta documentación no fueron compilados ni ejecutados por quien los escribió. Antes de usarlos en algo importante, compile el proyecto y pruebe los casos descritos en este documento.

## Índice

1. [¿Qué es CsvManager?](#1-qué-es-csvmanager)
2. [Conceptos necesarios](#2-conceptos-necesarios)
3. [Requisitos e instalación](#3-requisitos-e-instalación)
4. [Manual de uso](#4-manual-de-uso)
5. [El formato del archivo CSV](#5-el-formato-del-archivo-csv)
6. [Manejo de errores](#6-manejo-de-errores)
7. [Funcionamiento interno](#7-funcionamiento-interno)
8. [Decisiones de diseño](#8-decisiones-de-diseño)
9. [Limitaciones conocidas](#9-limitaciones-conocidas)
10. [Posibles dudas](#10-posibles-dudas)

---

# 1. ¿Qué es CsvManager?

Un programa guarda información en memoria (objetos, listas), y esa información **desaparece al cerrarlo**. Para conservarla hay que escribirla en un archivo.

`CsvManager` resuelve eso de forma automática:

```text
 Lista de objetos  ──  Write  ──▶  archivo .csv
 Lista de objetos  ◀──  Read  ──   archivo .csv
```

Lo importante es que **no hay que escribir código distinto para cada clase**. Basta con marcar qué campos se guardan y `CsvManager` se encarga del resto.

## Ejemplo mínimo

```csharp
using DataBases;

public class Persona
{
    [Atributos.Columna("id")]
    private int id;

    [Atributos.Columna("nombre")]
    private string nombre;

    public Persona(int id, string nombre)
    {
        this.id = id;
        this.nombre = nombre;
    }
}

// Guardar
var personas = new List<Persona> { new Persona(1, "Ana"), new Persona(2, "Luis") };
CsvManager.Write("personas.csv", personas);

// Leer
List<Persona> leidas = CsvManager.Read<Persona>("personas.csv");
```

El archivo `personas.csv` queda así:

```text
id,nombre
1,"Ana"
2,"Luis"
```

## Qué incluye

| Elemento | Función |
| --- | --- |
| `CsvManager.Write<T>` | Guarda una lista de objetos en un CSV. |
| `CsvManager.Read<T>` | Lee un CSV y devuelve una lista de objetos. |
| `CsvManager.ReadOrEmpty<T>` | Igual que `Read`, pero devuelve una lista vacía si el archivo no existe. |
| `CsvException` | Clase base de todos los errores propios. |
| `CsvFormatException` | Error en el contenido del archivo (con número de línea y columna). |
| `CsvConfiguracionException` | Error en cómo está definida la clase del programador. |

---

# 2. Conceptos necesarios

Esta sección explica, desde cero, las ideas que aparecen en el resto del documento. Quien ya las conozca puede saltarla.

## 2.1 Archivo CSV

*CSV* significa *Comma-Separated Values* ("valores separados por comas"). Es un **archivo de texto** donde cada línea es una fila y las columnas se separan con comas.

```text
id,nombre,edad
1,Ana,30
2,Luis,25
```

- La **primera línea** es el *encabezado*: los nombres de las columnas.
- Cada línea siguiente es un *registro* (una fila).

Se puede abrir con el Bloc de notas, con VS Code o con una planilla de cálculo.

## 2.2 Clase, objeto, campo, propiedad y constructor

| Término | Qué es | Ejemplo |
| --- | --- | --- |
| **Clase** | El molde que define cómo es algo. | `Persona` |
| **Objeto** | Una copia concreta creada a partir del molde. | La persona Ana |
| **Campo** | Una variable dentro de la clase. | `private int id;` |
| **Propiedad** | Un acceso a un valor, con `get` y/o `set`. | `public int Id => id;` |
| **Constructor** | El método especial que crea el objeto. | `public Persona(int id, string nombre)` |

> **Importante:** `CsvManager` guarda **campos**, no propiedades.

## 2.3 Atributo

Un *atributo* es una etiqueta que se coloca encima de algo (una clase, un campo) para agregarle información. No cambia lo que el código hace por sí mismo, pero otros programas pueden leerla.

```csharp
[Atributos.Columna("nombre")]   // ← atributo
private string nombre;
```

Esta etiqueta dice: "este campo se guarda en la columna llamada `nombre`".

## 2.4 Reflection

*Reflection* es la capacidad de un programa de **examinarse a sí mismo mientras se ejecuta**: preguntar qué campos tiene una clase, qué atributos tienen, qué constructores existen, y leer o asignar valores.

Analogía: un código normal es como usar un electrodoméstico a través de sus botones. Reflection es como abrir la carcasa para ver qué hay adentro.

`CsvManager` la usa para funcionar con **cualquier** clase sin conocerla de antemano:

```csharp
FieldInfo[] campos = typeof(Persona).GetFields(...);   // lista los campos de Persona
object? valor = campo.GetValue(unaPersona);            // lee el valor de un campo
```

**Contra:** el compilador no puede revisar lo que se escribe como texto (por ejemplo `"nombre"`). Los errores aparecen al ejecutar. Por eso `CsvManager` incluye muchas validaciones con mensajes claros.

## 2.5 Genéricos (`<T>`)

`Write<T>` y `Read<T>` son métodos *genéricos*. `T` es un "tipo a elegir al usarlo":

```csharp
CsvManager.Read<Persona>("personas.csv");   // T = Persona → devuelve List<Persona>
CsvManager.Read<Venta>("ventas.csv");       // T = Venta   → devuelve List<Venta>
```

## 2.6 `null`

`null` significa "ningún valor". No es lo mismo que cero ni que un texto vacío:

| Valor | Significado |
| --- | --- |
| `null` | No hay nada. |
| `""` | Hay un texto, pero no tiene caracteres. |
| `0` | Hay un número y vale cero. |

Un tipo seguido de `?` (como `int?` o `string?`) indica que puede valer `null`.

`CsvManager` conserva esta diferencia al guardar y al leer. Un `null` se escribe como la palabra `null`, sin comillas (ver sección 5.3).

## 2.7 Excepciones

Cuando algo sale mal, C# **lanza una excepción** (un objeto que describe el error). Si nadie la atrapa, el programa se detiene.

Para atraparla se usa `try / catch`:

```csharp
try
{
    // Código que puede fallar
}
catch (TipoDeError ex)
{
    // Qué hacer si falla. ex.Message contiene la descripción.
}
```

Los `catch` se revisan **de arriba hacia abajo** y entra el primero que coincide.

## 2.8 Datos externos y errores del programador

Existen dos orígenes de error muy distintos:

| Origen | Ejemplo | Quién lo corrige |
| --- | --- | --- |
| **Datos externos** (el archivo) | Una celda con `abc` donde va un número. | Quien editó el archivo. |
| **Configuración** (el código) | Una clase sin ningún `[Columna]`. | El programador. |

`CsvManager` los separa en dos excepciones distintas (sección 6).

## 2.9 Cultura invariante

Los países escriben los números de forma distinta. En Argentina, `3,5` es tres y medio; en Estados Unidos, `3.5`.

Si se guardara con una cultura y se leyera con otra, los datos se corromperían. Por eso `CsvManager` siempre usa `CultureInfo.InvariantCulture`: un formato fijo, igual en cualquier computadora.

Consecuencia práctica: **dentro del CSV los decimales siempre llevan punto.**

## 2.10 Ida y vuelta (*round-trip*)

Un sistema de guardado correcto cumple esto:

```text
objeto → Write → archivo → Read → objeto idéntico
```

Si al volver el objeto es distinto (por ejemplo, un texto vacío que vuelve como `null`), hay un error de ida y vuelta. Varias correcciones de este documento existen para evitarlos.

## 2.11 Caracteres de escape

Algunos caracteres tienen un significado especial en el CSV: la coma separa columnas y las comillas delimitan textos. ¿Cómo se guarda entonces un texto que contiene una coma o una comilla?

Se usa un *carácter de escape*: la barra invertida `\` avisa "el siguiente carácter se interpreta de forma especial o literal".

```text
Texto original:   Dijo "hola"
En el CSV:        "Dijo \"hola\""
```

## 2.12 Parser

Un *parser* es el código que lee un texto y lo descompone en sus partes. `ParsearLinea` toma una línea como `1,"Ana",30` y la separa en tres celdas: `1`, `Ana` y `30`.

## 2.13 Hilos y `lock`

Un programa puede hacer varias tareas a la vez, cada una en un *hilo*. Si dos hilos escriben el mismo archivo simultáneamente, el resultado puede corromperse. Un `lock` es una "puerta" que solo deja pasar a un hilo por vez.

## 2.14 Escritura atómica

Una operación es *atómica* si ocurre completa o no ocurre, sin estados intermedios. Si el programa se corta a mitad de guardar, el archivo no debe quedar medio escrito. `CsvManager` consigue esto escribiendo primero en un archivo temporal (sección 7.3).

---

# 3. Requisitos e instalación

## 3.1 Requisitos

| Requisito | Detalle |
| --- | --- |
| **.NET** | **.NET 8 o superior**: `DataBases.csproj` apunta a `net8.0`, por lo que la aplicación debe ser `net8.0` o superior (ver nota abajo). |
| **C#** | Versión 10 o superior (por `record struct`). |
| **Nullable** | Pensado para proyectos con `<Nullable>enable</Nullable>`. |

> **Nota sobre la versión de .NET.** Según mi conocimiento, `ArgumentException.ThrowIfNullOrWhiteSpace` existe solo desde .NET 8, y es la única llamada del código que lo exige (la conversión de enums usa `Enum.Parse`, disponible en todas las versiones). Si el proyecto usa .NET 6 o 7 y el compilador marca error en esas líneas, ver la sección 3.3. Este requisito no fue verificado compilando con .NET 6 o 7.

## 3.2 Instalación

La forma recomendada es agregar el proyecto como **referencia de proyecto** desde la aplicación de consola (pasos para Visual Studio y para la línea de comandos en el README, sección "Compilación e instalación en una aplicación de consola"). La alternativa es copiar los archivos, como se describe a continuación.

1. Si se copian los archivos: copiar la carpeta con los archivos al proyecto (si se usa la referencia de proyecto, omitir este paso).
2. Agregar el `using`:

```csharp
using DataBases;
```

Y, para escribir `[Columna("x")]` en lugar de `[Atributos.Columna("x")]`:

```csharp
using static DataBases.Atributos;
```

## 3.3 Si se copian los archivos a un proyecto con .NET 6 o 7

No hace falta con la referencia de proyecto (el proyecto apunta a `net8.0`). Si en cambio se copian los archivos a un proyecto con .NET 6 o 7, reemplazar la validación de la ruta, que aparece en `Write`, `Read` y `ReadOrEmpty`:

```csharp
// En lugar de:
ArgumentException.ThrowIfNullOrWhiteSpace(archivoPath);

// Usar:
if (archivoPath is null)
    throw new ArgumentNullException(nameof(archivoPath));
if (string.IsNullOrWhiteSpace(archivoPath))
    throw new ArgumentException("La ruta del archivo no puede estar vacía.", nameof(archivoPath));
```

---

# 4. Manual de uso

## 4.1 Preparar una clase

Para que una clase pueda guardarse y leerse, debe cumplir **cuatro reglas**.

| # | Regla | Por qué |
| --- | --- | --- |
| 1 | Cada dato a guardar es un **campo** (no una propiedad) con `[Columna("nombre")]`. | Reflection busca campos con esa etiqueta. |
| 2 | Debe existir un **constructor** con un parámetro por cada campo marcado. | Es la forma de reconstruir el objeto al leer. |
| 3 | El **nombre** de cada parámetro coincide con el del campo (mayúsculas no importan). | Así se sabe qué parámetro corresponde a qué campo. |
| 4 | El **tipo** de cada parámetro es idéntico al del campo. | Para convertir el texto al tipo correcto. |

### Ejemplo correcto

```csharp
public enum Estado { Activo, Inactivo }

public class Persona
{
    [Atributos.Columna("id")]
    private int id;

    [Atributos.Columna("nombre")]
    private string nombre;

    [Atributos.Columna("email")]
    private string? email;

    [Atributos.Columna("nacimiento")]
    private DateOnly nacimiento;

    [Atributos.Columna("estado")]
    private Estado estado;

    // Un parámetro por campo, mismo nombre, mismo tipo.
    public Persona(int id, string nombre, string? email, DateOnly nacimiento, Estado estado)
    {
        this.id = id;
        this.nombre = nombre;
        this.email = email;
        this.nacimiento = nacimiento;
        this.estado = estado;
    }

    // Las propiedades son para uso del programa; CsvManager no las guarda.
    public int Id => id;
    public string Nombre => nombre;
}
```

### Errores frecuentes al preparar una clase

| Error | Corrección |
| --- | --- |
| El campo se llama `_id` pero el parámetro `id`. | Que ambos se llamen igual (`id`). |
| El campo es `int` y el parámetro `long`. | Mismo tipo en ambos. |
| El constructor omite un campo. | Un parámetro por cada campo con `[Columna]`. |
| Se puso `[Columna]` sobre una propiedad. | Solo se admite sobre campos. |
| Dos campos con el mismo nombre de columna. | Cada nombre de columna debe ser único. |
| Un campo `List<int>`, un arreglo u otra clase. | Usar solo tipos soportados (ver abajo). Para relacionar objetos, usar el ORM. |

### Tipos soportados

| Categoría | Tipos |
| --- | --- |
| Texto | `string`, `char` |
| Lógicos | `bool` |
| Números | `byte`, `short`, `int`, `long`, `float`, `double`, `decimal` (y sus variantes sin signo) |
| Fecha y hora | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` |
| Otros | `Guid`, cualquier `enum` |
| Con valor nulo | La versión `?` de todos los anteriores (`int?`, `DateOnly?`, etc.) |

**No soportados:** listas, arreglos, diccionarios, otras clases como campo. Para relacionar objetos entre sí se usa el ORM (sección 4.8). Un campo de un tipo no soportado lanza `CsvConfiguracionException`.

**Validación de la clase:** en cada llamada a `Write`, `Read` y `ReadOrEmpty` se valida la clase completa (campos, tipos y constructor), aunque el archivo esté vacío o no exista. Los errores de configuración aparecen siempre, no solo cuando hay datos.

## 4.2 Guardar: `Write<T>`

```csharp
CsvManager.Write("datos/personas.csv", personas);
```

| Aspecto | Comportamiento |
| --- | --- |
| **Archivo existente** | Se **reemplaza por completo** con el contenido de la lista. |
| **Carpeta inexistente** | Se crea automáticamente. |
| **Lista vacía** | Se escribe solo el encabezado. |
| **Elemento `null` en la lista** | Lanza `ArgumentException`. |
| **Campo con valor `null`** | Se escribe la palabra `null`, sin comillas. |
| **Clase mal definida** | Lanza `CsvConfiguracionException` antes de escribir. También se exige un constructor compatible, para no guardar un archivo que luego no se pueda leer. |
| **Enum sin `[Flags]` con un valor no definido** | Lanza `ArgumentException`: ese valor no podría leerse de vuelta. |
| **Cortes durante el guardado** | El archivo anterior queda intacto (ver 7.3). |
| **Codificación** | UTF-8 sin BOM. |

> **Importante:** `Write` guarda **toda** la lista cada vez. Para agregar un registro hay que agregarlo a la lista en memoria y volver a guardar la lista completa.

## 4.3 Leer: `Read<T>` y `ReadOrEmpty<T>`

```csharp
// Lanza FileNotFoundException si el archivo no existe
List<Persona> personas = CsvManager.Read<Persona>("datos/personas.csv");

// Devuelve una lista vacía si el archivo no existe
List<Persona> personas = CsvManager.ReadOrEmpty<Persona>("datos/personas.csv");
```

| Situación | `Read<T>` | `ReadOrEmpty<T>` |
| --- | --- | --- |
| El archivo no existe | `FileNotFoundException` | Lista vacía |
| Archivo vacío | Lista vacía | Lista vacía |
| Solo encabezado | Lista vacía | Lista vacía |
| Contenido inválido | `CsvFormatException` | `CsvFormatException` |

**¿Cuál elegir?** Usar `ReadOrEmpty` al **iniciar el programa**, cuando es normal que todavía no exista el archivo. Usar `Read` cuando la ausencia del archivo sea un problema real.

`ReadOrEmpty` solo comprueba si el archivo existe; el resto de los errores se siguen lanzando.

Aunque el archivo esté vacío o no exista, la clase `T` se valida igualmente: una clase mal definida lanza `CsvConfiguracionException` en esos casos también.

## 4.4 Ejemplo completo

```csharp
using DataBases;
using static Utilidades.DecoracionConsola;

const string ruta = "datos/personas.csv";

List<Persona> personas;

try
{
    personas = CsvManager.ReadOrEmpty<Persona>(ruta);
}
catch (CsvFormatException ex)
{
    ImprimirMesajeDeError(ex.Message);
    return;
}
catch (CsvConfiguracionException ex)
{
    ImprimirMesajeDeError("Error de programación: " + ex.Message);
    return;
}

personas.Add(new Persona(personas.Count + 1, "Marta", null,
                         new DateOnly(1985, 3, 10), Estado.Activo));

CsvManager.Write(ruta, personas);
ImprimirMesajeDeExito("Datos guardados.");
```

## 4.5 Herencia

Los campos con `[Columna]` de las **clases base** también se guardan, incluso si son privados.

```csharp
public class Persona
{
    [Atributos.Columna("id")]
    private int id;
    // ...
}

public class Empleado : Persona
{
    [Atributos.Columna("sueldo")]
    private decimal sueldo;

    // El constructor debe recibir TODOS los campos: id y sueldo.
    public Empleado(int id, decimal sueldo) : base(id)
    {
        this.sueldo = sueldo;
    }
}
```

Orden de columnas al escribir: primero las de la clase base y luego las de la derivada (`id,sueldo`).

## 4.6 Campos opcionales (`null`)

Para permitir "sin valor", declarar el campo y el parámetro con `?`:

```csharp
[Atributos.Columna("telefono")]
private string? telefono;      // puede ser null

[Atributos.Columna("edad")]
private int? edad;             // puede ser null
```

En el archivo, el valor ausente se escribe como la palabra `null` sin comillas (ver sección 5.3):

```text
id,telefono,edad
1,"555-1234",30
2,null,null
```

Si un campo **no** admite null (`int`) y la celda contiene `null` (o está vacía sin comillas), `Read` lanza un error indicando línea y columna.

> Un `string` declarado sin `?` puede recibir `null` igualmente en ejecución, porque C# no lo impide. Para evitarlo, usar `string?` o comprobar en el constructor.

## 4.7 Varias tablas

Cada clase se guarda en su propio archivo:

```csharp
CsvManager.Write("datos/clientes.csv", clientes);
CsvManager.Write("datos/ventas.csv",   ventas);
```

Al iniciar, cargar **todas** las tablas antes de usar relaciones del ORM:

```csharp
var clientes = CsvManager.ReadOrEmpty<Cliente>("datos/clientes.csv");
var ventas   = CsvManager.ReadOrEmpty<Venta>("datos/ventas.csv");
```

`CsvManager` no coordina varios archivos: si se guardan dos y el segundo falla, el primero ya quedó guardado.

## 4.8 Uso junto con el ORM

`CsvManager` guarda datos; la clase `ORM` (en `ORM.cs`) resuelve relaciones entre objetos ya cargados. Funcionan juntos:

```csharp
public class Venta
{
    [Atributos.Columna("id")]        private int id;
    [Atributos.Columna("clienteId")] private int clienteId;

    private Cliente? cliente;

    [Atributos.Relacion_1_1("clienteId", "id")]
    public Cliente Cliente => cliente ??= ORM.Relacionar_1_1(this, nameof(Cliente), Datos.Clientes)!;
    // ...
}
```

Solo el campo `clienteId` se guarda en el CSV. El objeto `Cliente` se busca en memoria la primera vez que se accede a la propiedad.

## 4.9 Buenas prácticas

- **Un constructor por clase** con exactamente los campos persistidos. Evita ambigüedades.
- **Nombres de columna cortos, sin espacios ni símbolos.** Se restringen la coma, las comillas, la barra `\` y los saltos de línea.
- **Capturar `CsvFormatException`** y mostrar `ex.Message` al usuario: ya incluye línea y columna.
- **Al editar a mano, escribir `null` en minúscula y sin comillas** para un valor ausente.
- **No editar los CSV con Excel**. Preferir un editor de texto.
- **Hacer copias de seguridad** de la carpeta de datos antes de cambios grandes.
- **No cambiar el nombre de una columna** (`[Columna("x")]`) sin migrar los archivos existentes: los CSV viejos dejarán de leerse.

---

# 5. El formato del archivo CSV

`CsvManager` usa un formato propio, **inspirado en CSV pero no idéntico al estándar**. Conviene conocerlo para editar archivos a mano o depurar.

## 5.1 Estructura

```text
id,nombre,email,nacimiento,estado
1,"Ana Pérez","ana@mail.com",1990-05-17,"Activo"
2,"Luis",null,2001-12-03,"Inactivo"
```

- **Línea 1:** encabezado con los nombres definidos en `[Columna]`.
- **Líneas siguientes:** un objeto por línea.
- **Separador:** coma `,`.
- **Orden de columnas al leer:** no importa; se asocian por nombre.
- **Líneas en blanco:** se ignoran.

## 5.2 Cómo se escribe cada tipo

| Tipo | Ejemplo en el archivo |
| --- | --- |
| `string` | `"Ana Pérez"` (siempre entre comillas) |
| `char` | `"x"` (entre comillas) |
| `enum` | `"Activo"` (entre comillas, se guarda el **nombre**) |
| `int`, `long`, etc. | `42` |
| `double`, `decimal` | `3.5` (siempre con **punto**) |
| `bool` | `True` o `False` |
| `DateTime` | `2026-10-01T14:30:00.0000000` |
| `DateOnly` | `2026-10-01` |
| `TimeOnly` | `14:30:00.0000000` |
| `TimeSpan` | `1.02:03:04` (días.horas:minutos:segundos) |
| `Guid` | `3f2504e0-4f89-11d3-9a0c-0305e82c3301` |
| `null` | `null` (la palabra, sin comillas) |

## 5.3 `null` frente a texto vacío

| En el archivo | Significa |
| --- | --- |
| `null` (sin comillas) | `null` |
| `""` (comillas vacías) | Texto vacío `""` |
| `"null"` (con comillas) | El texto `null` |
| Celda vacía sin comillas (`,,`) | `null` (solo al leer; `Write` nunca la produce) |

Reglas:

- La palabra `null` se reconoce solo en **minúscula** y **sin comillas**. `Null` o `NULL` sin comillas no son `null`: se intentan convertir al tipo del campo y fallan (o, en un `string`, se leen como texto).
- Esta diferencia solo funciona si el archivo **conserva las comillas**. Si una herramienta las quita, `""` se leerá como `null` y `"null"` también.
- La celda vacía sin comillas se sigue aceptando como `null` para que los archivos escritos con versiones anteriores se lean sin cambios. Al volver a guardarlos quedan con la palabra `null`.

## 5.4 Caracteres especiales dentro de textos

| Carácter | Se escribe |
| --- | --- |
| `\` | `\\` |
| `"` | `\"` |
| Salto de línea | `\n` |
| Retorno de carro | `\r` |
| Tabulación | `\t` |

Ejemplo: el texto `Dijo "hola"` seguido de un salto de línea se guarda como:

```text
"Dijo \"hola\"\n"
```

Así, **cada registro ocupa siempre una sola línea del archivo**, aunque el texto original tenga varios renglones.

## 5.5 Diferencias con el CSV estándar

| Aspecto | CSV estándar (RFC 4180) | CsvManager |
| --- | --- | --- |
| Comilla dentro de un texto | Se duplica: `""` | Se escapa: `\"` |
| Saltos de línea en un texto | Permitidos dentro de comillas | Se escriben como `\n` |
| Separador | Varía según región (`,` o `;`) | Siempre `,` |
| Valor nulo | No existe; suele ser una celda vacía | La palabra `null` sin comillas |

Consecuencia: un archivo escrito por otro programa con comillas duplicadas (`""`) **no se lee correctamente**.

## 5.6 Editar a mano

Reglas para no romper el archivo:

1. No cambiar el encabezado, salvo para reordenar columnas (se puede, siempre que estén todas).
2. Poner los textos entre comillas, sobre todo si contienen comas.
3. Usar punto decimal (`3.5`, no `3,5`).
4. Cada fila debe tener exactamente tantas celdas como el encabezado.
5. Respetar los formatos de fecha de la tabla 5.2.
6. Guardar en UTF-8.
7. Escribir los valores ausentes como `null` (minúscula, sin comillas).

---

# 6. Manejo de errores

## 6.1 Jerarquía de excepciones

```text
Exception
 └── CsvException                     ← atrapa todos los errores propios
      ├── CsvFormatException          ← el ARCHIVO tiene un problema
      │     ├── Linea   (int)
      │     └── Columna (string?)
      └── CsvConfiguracionException   ← la CLASE está mal definida
```

Además, pueden aparecer errores estándar de .NET:

| Excepción | Cuándo |
| --- | --- |
| `FileNotFoundException` | `Read` sobre un archivo que no existe. |
| `ArgumentNullException` | Se pasó `null` como lista o como ruta. |
| `ArgumentException` | Ruta vacía o solo espacios; la lista contiene un `null`; o, al escribir, un enum sin `[Flags]` con un valor no definido. (`ArgumentNullException` hereda de esta.) |
| `UnauthorizedAccessException` | Sin permisos sobre el archivo o la carpeta. |
| `IOException` | Archivo en uso, disco lleno, ruta inválida. |

Los errores de entrada y salida (`IOException`, `UnauthorizedAccessException`) **no se envuelven**: llegan tal como los lanza .NET.

## 6.2 Patrón de captura recomendado

```csharp
try
{
    var personas = CsvManager.Read<Persona>(ruta);
}
catch (FileNotFoundException)
{
    // El archivo no existe todavía.
}
catch (CsvFormatException ex)
{
    // Mostrar ex.Message al usuario (ya incluye línea y columna).
    // ex.Linea y ex.Columna están disponibles por separado.
}
catch (CsvConfiguracionException ex)
{
    // Error de programación: corregir la clase.
}
catch (IOException ex)
{
    // Archivo en uso, disco, etc.
}
catch (UnauthorizedAccessException ex)
{
    // Permisos.
}
```

El orden importa: poner `catch (CsvException)` primero atraparía todo y dejaría inútiles a los siguientes.

## 6.3 Catálogo de mensajes

### `CsvConfiguracionException` — corregir el código

| Mensaje | Causa | Solución |
| --- | --- | --- |
| `La clase X no tiene ningún campo con [Columna].` | Ningún campo está marcado. | Agregar `[Columna("...")]` a los campos a guardar. |
| `El nombre de columna 'n' (X.campo) está vacío o contiene caracteres no permitidos...` | El nombre es vacío o tiene `,` `"` `\` o saltos de línea. | Usar un nombre simple. |
| `X tiene nombres de columna repetidos: a, b.` | Dos campos usan el mismo nombre. | Dar nombres distintos. |
| `El tipo T del campo X.campo no está soportado.` | El campo es una lista, un arreglo, un objeto u otro tipo no soportado. | Usar un tipo soportado, o guardar la relación en otra tabla (ORM). |
| `No se encontró un constructor compatible para X. Se necesita uno con los parámetros (...)` | Ningún constructor coincide con los campos. | Crear uno con un parámetro por campo, mismo nombre y tipo. El mensaje lista la firma esperada. |

### `CsvFormatException` — corregir el archivo

Todos comienzan con `Línea N:` o `Línea N, columna 'c':`.

| Mensaje | Causa | Solución |
| --- | --- | --- |
| `Parece que el archivo usa ';' como separador...` | El archivo se guardó con punto y coma (típico de Excel en español). | Reemplazar `;` por `,` o regenerar el archivo. |
| `Columnas duplicadas en el encabezado: ...` | Una columna aparece dos veces. | Eliminar la repetida. |
| `Las columnas [...] no existen en X.` | El encabezado tiene nombres que la clase no define. | Corregir el nombre o quitar la columna. |
| `Faltan las columnas [...] requeridas por X.` | La clase tiene campos que no están en el archivo. | Agregar las columnas. |
| `Se esperaban N columnas pero hay M.` | La fila tiene más o menos celdas que el encabezado. | Revisar comas sobrantes o faltantes. |
| `La línea termina con una barra '\' incompleta.` | Una `\` al final de la línea. | Duplicarla (`\\`) o quitarla. |
| `Hay comillas sin cerrar.` | Número impar de comillas. | Cerrar la comilla. |
| `La celda es null pero X no admite null.` | `null` (o celda vacía) en un campo no nulo. | Completar el valor, o hacer nulo el campo (`int?`). |
| `No se pudo convertir 'abc' a Int32: ...` | El texto no corresponde al tipo. | Corregir el dato. |
| `'x' no es un valor válido de Estado. Valores: ...` | El texto no es un valor del enum. | Usar uno de los valores listados. |
| `El constructor de X rechazó los datos: ...` | El constructor lanzó una excepción propia (una validación). | Corregir el dato según el mensaje. |

### Errores al escribir (`Write`)

| Excepción y mensaje | Causa | Solución |
| --- | --- | --- |
| `ArgumentException`: `La lista contiene un elemento null.` | Un elemento de la lista es `null`. | Quitarlo antes de guardar. |
| `ArgumentException`: `El valor V no está definido en el enum E; no podría leerse de vuelta.` | Un enum sin `[Flags]` tiene un valor fuera de su lista (por ejemplo `(Estado)99`). | Asignar un valor definido. |
| `CsvConfiguracionException` | La clase está mal definida (ver la tabla anterior). | Corregir la clase. |

## 6.4 Cómo diagnosticar

```text
¿Qué excepción apareció?
│
├─ CsvConfiguracionException → el problema está en la CLASE. Revisar sección 4.1.
│
├─ CsvFormatException        → el problema está en el ARCHIVO.
│     └─ Abrirlo en un editor de texto e ir a la línea indicada.
│
├─ FileNotFoundException     → la ruta es incorrecta o el archivo no existe aún.
│
└─ IOException / UnauthorizedAccessException
      → archivo abierto en otro programa, o sin permisos.
```

---

# 7. Funcionamiento interno

## 7.1 Mapa de métodos

```text
CsvManager
│
├── Públicos
│    ├── Write<T>(ruta, lista)
│    ├── Read<T>(ruta)
│    └── ReadOrEmpty<T>(ruta)
│
└── Privados (de apoyo)
     ├── Reflection
     │    ├── NombreColumna(campo)
     │    ├── ObtenerCampos(tipo)
     │    ├── BuscarConstructor(tipo, campos)
     │    ├── ValidarClase(tipo)          (ObtenerCampos + BuscarConstructor)
     │    ├── EsTipoSoportado(tipo)
     │    └── AceptaNull(tipo)
     ├── Escritura
     │    ├── FormatearValor(valor, tipo)
     │    └── EscaparString(texto)
     └── Lectura
          ├── ParsearLinea(linea, nº)
          └── ConvertirValor(celda, tipo, nº, columna)
```

## 7.2 `ObtenerCampos`: descubrir qué guardar

Devuelve los campos de `T` marcados con `[Columna]` y valida la configuración.

```text
1. Recorrer la jerarquía: T, su clase base, la base de esa, ... hasta object.
2. Invertir la lista (la clase base primero).
3. De cada clase tomar sus campos DECLARADOS (DeclaredOnly), públicos y privados.
4. Quedarse con los que tienen [Columna].
5. Validar:
     · hay al menos uno
     · los nombres no están vacíos ni tienen caracteres prohibidos
     · el tipo de cada campo está soportado (EsTipoSoportado)
     · los nombres no se repiten
```

**Por qué `DeclaredOnly`:** `GetFields` con `NonPublic` no devuelve los campos *privados* heredados, así que hay que consultar cada clase de la jerarquía por separado. Con `DeclaredOnly` cada clase aporta solo los suyos y no hay duplicados.

## 7.3 `Write<T>`: guardar

```text
1. Validar argumentos (ruta no vacía, lista no null).
2. ValidarClase(typeof(T)): ObtenerCampos + BuscarConstructor.
3. Construir la lista de líneas:
     a. Encabezado: los nombres de columna unidos por comas.
     b. Para cada objeto:
          · Leer el valor de cada campo con campo.GetValue(objeto)
          · Convertirlo a texto con FormatearValor
          · Unir los textos con comas
4. Dentro de un lock:
     a. Crear la carpeta si no existe.
     b. Escribir todas las líneas en "archivo.csv.tmp".
     c. Reemplazar "archivo.csv" por el temporal (File.Move con overwrite).
     d. Si algo falla en (b) o (c), borrar el temporal y relanzar el error.
```

**Por qué hay un archivo temporal.** `File.WriteAllLines` directamente sobre el archivo real **borra el contenido anterior y luego escribe**. Si el programa se corta en medio, se pierde el archivo. Con el temporal:

```text
Sin temporal:   [archivo viejo] → [vacío...] → [escritura a medias]  ← corte = datos perdidos
Con temporal:   [archivo viejo] intacto
                [archivo.tmp] se escribe completo
                un solo paso: .tmp reemplaza al viejo
```

El reemplazo es una operación única del sistema operativo, mucho menos propensa a quedar a medias.

**El `lock`.** Hay un único objeto `bloqueo` compartido por toda la clase. Mientras un hilo escribe o lee, los demás esperan. Es simple pero tiene un efecto secundario: leer el archivo A puede esperar a que termine de escribirse el archivo B (ver sección 10).

## 7.4 `FormatearValor`: de valor a texto

```text
¿null?                         → null (la palabra, sin comillas)
¿enum sin [Flags] con valor no definido? → error (ArgumentException)
¿string, char o enum?          → "texto escapado" (entre comillas)
¿DateTime o DateTimeOffset?    → formato "O"  (2026-10-01T14:30:00.0000000)
¿DateOnly o TimeOnly?          → formato "O"
¿TimeSpan?                     → formato "c"  (1.02:03:04)
¿IFormattable (números...)?    → ToString(null, InvariantCulture)
cualquier otro                 → Convert.ToString(valor, InvariantCulture)
```

**Por qué el orden importa:** `DateTime` también es `IFormattable`. Si el caso general estuviera antes, atraparía todo y las fechas perderían su formato propio.

**Por qué `char` y enum van entre comillas:** un `char` podría ser una coma o una comilla, y un enum con `[Flags]` puede escribirse como `A, B` (con coma). Sin comillas romperían la fila.

## 7.5 `EscaparString`: proteger caracteres especiales

Recorre el texto carácter por carácter y reemplaza los especiales (tabla 5.4). Es lo que permite que un texto con comas, comillas o saltos de línea quede en **una sola celda** y **una sola línea**.

## 7.6 `Read<T>`: leer

```text
 1. Validar la ruta y comprobar que el archivo existe (si no: FileNotFoundException).
 2. ObtenerCampos(typeof(T)): la clase se valida siempre, aunque el archivo esté vacío.
 3. Leer todas las líneas dentro de un lock.
 4. Buscar la primera línea con contenido. Si no hay ninguna →
    BuscarConstructor (valida el constructor) y devolver lista vacía.
 5. ParsearLinea del encabezado y quitar espacios de los nombres.
 6. Validar el encabezado:
      · ¿Una sola columna con ';'?        → error de separador
      · ¿Columnas duplicadas?             → error
      · ¿Columnas que la clase no tiene?  → error
      · ¿Columnas de la clase que faltan? → error
 7. Armar camposOrdenados: para cada columna del CSV, su campo.
 8. BuscarConstructor (una sola vez) → constructor + índices.
 9. Para cada fila:
      a. Si está en blanco, saltar.
      b. ParsearLinea.
      c. ¿Cantidad de celdas = cantidad de columnas? Si no → error.
      d. Para cada parámetro del constructor: ConvertirValor de la celda correspondiente.
      e. constructor.Invoke(argumentos) y agregar a la lista.
10. Devolver la lista.
```

**Principio:** primero se valida todo lo que depende de la **estructura** (pasos 2 a 8), una sola vez; luego lo que depende de **cada fila** (paso 9), con su número de línea.

## 7.7 `BuscarConstructor`: emparejar columnas con parámetros

Para reconstruir un objeto hay que llamar a su constructor con los valores en el orden correcto de **parámetros**, pero las celdas están en el orden de **columnas** del archivo. Hay que traducir.

```text
Columnas del CSV:        [nombre, id, email]       (índices 0, 1, 2)
Constructor:             Persona(int id, string nombre, string? email)

Para cada parámetro, se busca la columna cuyo campo tenga:
   · el mismo nombre (ignorando mayúsculas)
   · el mismo tipo

indices = [1, 0, 2]
           │  │  └── parámetro 2 (email)  ← columna 2
           │  └───── parámetro 1 (nombre) ← columna 0
           └──────── parámetro 0 (id)     ← columna 1
```

Por eso el orden de las columnas en el archivo no importa.

Si ningún constructor coincide, el mensaje de error muestra la firma que se necesita.

**Constructores considerados:** públicos y no públicos, solo de instancia.

## 7.8 `ParsearLinea`: separar una línea en celdas

Recorre la línea carácter por carácter con tres banderas:

| Bandera | Se activa con | Efecto |
| --- | --- | --- |
| `escapado` | `\` | El **siguiente** carácter se interpreta de forma especial (`n` → salto de línea, `"` → comilla literal). |
| `dentroDeString` | `"` (alterna) | Mientras está activa, una coma **no** separa celdas. |
| `huboComillas` | Cualquier `"` | Se recuerda para distinguir `""` y `"null"` de una celda vacía y de `null`. |

### Ejemplo paso a paso

Línea: `1,"Ana \"la\" Pérez",null,25`

| Carácter | Qué ocurre | Celdas terminadas |
| --- | --- | --- |
| `1` | Se acumula en el valor actual. | |
| `,` | Fuera de comillas: termina la celda. | `1` |
| `"` | Abre comillas; `huboComillas = true`. | |
| `Ana` | Se acumula. | |
| `\` | `escapado = true`. | |
| `"` | Con `escapado`, se agrega como comilla literal. | |
| `la` | Se acumula. | |
| `\` y `"` | Otra comilla literal. | |
| `Pérez` | Se acumula. | |
| `"` | Cierra comillas. | |
| `,` | Fuera de comillas: termina la celda. | `Ana "la" Pérez` *(con comillas)* |
| `null` | Se acumula (sin comillas). | |
| `,` | Fuera de comillas: termina la celda. | `null` *(sin comillas)* |
| `2`, `5` | Se acumulan. | |
| *(fin)* | Se agrega la última celda. | `25` |

Resultado: 4 celdas. El parser no interpreta `null`: la celda queda como texto `null` con `EntreComillas = false`, y es `ConvertirValor` quien la convierte en `null` (sección 7.9).

### Validaciones finales

Al terminar el recorrido:

- Si `escapado` sigue activo → la línea terminó con `\`. Error.
- Si `dentroDeString` sigue activo → falta una comilla de cierre. Error.

## 7.9 `ConvertirValor`: de texto a valor

```text
Entrada: una Celda (texto + si tenía comillas), el tipo destino.

1. Determinar:
     · ¿el tipo acepta null? (clase, o Nullable<T>)
     · el tipo "real" (sin Nullable)

2. ¿Celda SIN comillas cuyo texto es "null" o está vacío?
     ├─ El tipo acepta null → devolver null
     └─ No acepta null      → error "la celda es null"

3. Según el tipo real:
     string           → el texto tal cual
     char             → exige exactamente 1 carácter
     enum             → Enum.Parse + comprobar que el valor esté definido
                        (los [Flags] se aceptan sin esa comprobación)
     DateTime         → DateTime.Parse(..., RoundtripKind)
     DateTimeOffset   → DateTimeOffset.Parse(..., RoundtripKind)
     DateOnly         → DateOnly.Parse
     TimeOnly         → TimeOnly.Parse
     TimeSpan         → TimeSpan.Parse
     Guid             → Guid.Parse
     bool             → bool.Parse
     cualquier otro   → Convert.ChangeType (números, etc.)

4. Si .NET lanza FormatException, OverflowException, InvalidCastException
   o ArgumentException → se envuelve en CsvFormatException con línea y columna.
```

**Por qué `RoundtripKind` en las fechas:** conserva la información de si la hora era local, UTC o sin especificar, de modo que la fecha leída sea idéntica a la guardada.

**Por qué se rechazan enums numéricos no definidos:** `Enum.Parse` acepta cualquier número (`"99"`) aunque no corresponda a ningún valor del enum, y produce un valor "fantasma". Aquí se considera un error.

## 7.10 Invocación del constructor

```csharp
datos.Add((T)constructor.Invoke(argumentos));
```

Si el constructor lanza una excepción (por ejemplo, una validación de edad), .NET la envuelve en una `TargetInvocationException`. El mensaje útil está en `InnerException`, así que se extrae y se reenvía como `CsvFormatException` con la línea del archivo.

---

# 8. Decisiones de diseño

Estas elecciones son razonables pero discutibles. Se documentan para poder cambiarlas con conocimiento.

| Decisión | Alternativa | Motivo de la elección |
| --- | --- | --- |
| Archivo vacío → lista vacía | Lanzar error | Un archivo vacío es habitual al empezar. |
| `Read` lanza si no existe archivo | Devolver lista vacía siempre | Distinguir "no hay datos" de "ruta equivocada". `ReadOrEmpty` cubre el otro caso. |
| Faltan columnas → error | Usar valores por defecto | Evita datos incompletos sin aviso. |
| Escribir `null` explícito, sin comillas | Celda vacía | Se ve a simple vista en el archivo, y con comillas (`"null"`) sigue siendo un texto normal. |
| Seguir aceptando la celda vacía como `null` al leer | Rechazarla | Los archivos escritos con versiones anteriores se leen sin cambios. |
| Validar siempre la clase (campos, tipos y constructor) | Validar solo cuando hay datos | Los errores de configuración aparecen siempre y no solo cuando el archivo tiene filas. |
| `Write` exige un constructor compatible | Permitir clases solo de escritura | Evita guardar un archivo que luego no se podría leer. |
| Rechazar los tipos no soportados | Convertirlos con `ToString` | Un texto como `List`1[Int32]` no se puede leer de vuelta. |
| Formato propio con `\"` | CSV estándar con `""` | Más simple de parsear y de escribir en una sola línea. |
| Un único `lock` para todo | Un lock por archivo | Simplicidad. |
| Guardar siempre la lista completa | Agregar solo el registro nuevo | Mantiene el archivo consistente con la memoria. |
| Constructor obligatorio | Crear el objeto vacío y asignar los campos por Reflection | Respeta las validaciones del constructor y permite campos `readonly`. |
| Solo campos, no propiedades | Soportar propiedades | Las propiedades auto-implementadas tienen campos internos con nombres generados. |

---

# 9. Limitaciones conocidas

| Limitación | Detalle |
| --- | --- |
| **Concurrencia entre programas** | El `lock` protege solo dentro de un mismo proceso. |
| **Un único `lock` global** | Leer un archivo puede esperar a que termine de escribirse otro distinto. |
| **Archivos grandes** | `ReadAllLines` carga el archivo completo en memoria. |
| **Sin transacciones** | Guardar varias tablas no es atómico en conjunto. |
| **Constructor obligatorio** | Debe coincidir con los campos persistidos. |
| **Tipos complejos** | No hay listas, arreglos ni objetos anidados como columna. |
| **Formato propio** | No es el CSV estándar (ver 5.5). |
| **Cambios de estructura** | Renombrar o agregar columnas a una clase deja inservibles los archivos viejos. |
| **Rendimiento** | Cada lectura recorre todo el archivo; no hay índices. |
| **Excel** | Puede alterar el archivo al guardarlo |
| **Constructor para escribir** | `Write` también exige un constructor compatible, aunque la clase solo se vaya a guardar. |
| **Enums no definidos** | Un enum sin `[Flags]` con un valor fuera de su lista no se puede guardar. |

---

# 10. Posibles dudas

### 10.1 ¿Por qué no se guarda mi propiedad?

`CsvManager` solo guarda **campos** marcados con `[Columna]`. Crear un campo, marcarlo y, si hace falta, exponerlo con una propiedad (`public int Id => id;`).

### 10.2 ¿Por qué dice "No se encontró un constructor compatible"?

Ningún constructor tiene un parámetro por cada campo, con **el mismo nombre** (mayúsculas no importan) y **el mismo tipo**. El mensaje indica la firma esperada. Causas típicas: un campo `_id` frente a un parámetro `id`, un tipo distinto (`int` frente a `long`) o un campo sin parámetro.

### 10.3 ¿Puedo agregar un campo nuevo a una clase que ya tiene datos guardados?

No directamente. `Read` exige que estén **todas** las columnas. Opciones: agregar a mano la columna al CSV (con valores en cada fila), o escribir un programa de migración que lea con la clase vieja y guarde con la nueva.

### 10.4 ¿Puedo abrir los CSV con Excel?

Se pueden abrir para **mirar**, pero se recomienda no guardarlos desde Excel porque:

- Con la configuración regional de muchos países hispanohablantes, Excel guarda con `;` (el programa lo detecta y avisa).
- Excel reescribe el formato de las comillas y de las fechas.
- Puede quitar las comillas de los textos, con lo que `""` y `"null"` pasarían a leerse como `null`.
- Excel puede interpretar mal los acentos si el archivo es UTF-8 sin BOM.

Es preferible editar con un editor de texto (VS Code, Notepad++, Bloc de notas).

### 10.5 ¿Cómo agrego un registro nuevo?

Agregarlo a la lista en memoria y llamar a `Write` con la lista completa.

### 10.6 ¿Qué pasa si dos partes del programa escriben a la vez?

Dentro del mismo programa, el `lock` los pone en fila. Entre **programas distintos**, no hay protección.

### 10.7 ¿Qué pasa si se corta la luz mientras se guarda?

El archivo original no se toca hasta que el temporal esté completo, así que debería permanecer íntegro. Puede quedar un archivo `.tmp` suelto; se puede borrar sin problema (se sobrescribe en el próximo guardado).

### 10.8 ¿Se puede guardar una lista dentro de un objeto?

No directamente. Se guardan en tablas separadas relacionadas por un identificador y se enlazan con el ORM (sección 4.8).

### 10.9 ¿Por qué mi `string` vuelve como `null`?

Posiblemente la celda está escrita como `null` sin comillas, o vacía sin comillas. Para que sea un texto vacío debe estar escrita como `""`, y para que sea el texto `null`, como `"null"`. Si el archivo fue editado por otro programa, puede haber perdido las comillas.

### 10.10 ¿Por qué mis decimales dan error?

En el archivo deben llevar **punto** (`3.5`), no coma. `CsvManager` siempre usa la cultura invariante.

### 10.11 ¿Puedo cambiar el nombre de una columna?

Sí, pero entonces hay que cambiar también el encabezado de todos los archivos existentes, o dejarán de leerse.

### 10.12 ¿Puedo guardar un enum por número en vez de por nombre?

No. Se guarda el nombre (`"Activo"`). Es más legible y resiste cambios en el orden del enum. Al renombrar un valor del enum hay que actualizar los archivos.

### 10.13 ¿Hace falta un constructor sin parámetros?

No. Al contrario: se necesita un constructor **con** parámetros para todos los campos persistidos.

### 10.14 ¿Se puede usar con `record` o `struct`?

La lectura funciona con tipos que tengan un constructor compatible. Los `record` posicionales declaran propiedades en lugar de campos con `[Columna]`, por lo que no son directamente compatibles. No fue probado con `struct`.

### 10.15 ¿Por qué un error en un constructor aparece como `CsvFormatException`?

Porque normalmente significa que **un dato del archivo no pasó una validación** del constructor. El mensaje incluye el texto original de la excepción y la línea afectada.

### 10.16 ¿Es seguro para datos sensibles (contraseñas)?

No. El archivo es texto plano, sin cifrado. No guardar información sensible sin protegerla aparte.

### 10.17 ¿Cómo se guarda un `null`?

Como la palabra `null`, en minúscula y sin comillas:

```text
id,nombre,email
1,"Ana",null
2,"Luis","luis@mail.com"
```

Al leer, esa palabra se convierte en `null` si el campo lo admite (`string`, `int?`, etc.) y produce un error con línea y columna si no lo admite (`int`). Un texto cuyo contenido es `null` se guarda con comillas (`"null"`) y vuelve como texto. Detalle en la sección 5.3.
