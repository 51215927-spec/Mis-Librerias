# Mis Librerías

Dos librerías de C# para programas de consola: una para **entrada de datos validada, menús y formato de salida**, y otra para **persistir objetos en archivos CSV y relacionarlos en memoria**. No tienen dependencias externas.

| Librería | Namespace | Para qué sirve | Clases públicas |
| :--- | :--- | :--- | :--- |
| **Utilidades** | `Utilidades` | Pedir datos con validación, menús con flechas, mensajes de colores, tablas y marcos. | `Funciones`, `DecoracionConsola`, `MyConsole` |
| **DataBases** | `DataBases` | Guardar y leer `List<T>` en CSV, y resolver relaciones entre objetos cargados. | `CsvManager`, `ORM`, `Atributos`, `CsvException` y derivadas |

Las dos librerías son independientes: `DataBases` no usa `Utilidades` ni al revés.

> **Estado de verificación:** el código y la documentación de estas librerías no fueron compilados ni ejecutados por quien los escribió. Compile la solución y pruebe los casos de uso antes de emplearlas en algo importante.

---

## Contenido

1. [Requisitos](#requisitos)
2. [Estructura de la solución](#estructura-de-la-solución)
3. [Compilación e instalación en una aplicación de consola](#compilación-e-instalación-en-una-aplicación-de-consola)
4. [Utilidades](#utilidades)
5. [DataBases](#databases)
6. [Limitaciones principales](#limitaciones-principales)
7. [Documentación detallada](#documentación-detallada)

---

## Requisitos

| Elemento | Detalle |
| :--- | :--- |
| Framework de los proyectos | `net8.0` (ver `TargetFramework` en cada `.csproj`) |
| SDK para compilar | .NET 8 o superior. El archivo `Mis Librerias.slnx` requiere además un SDK reciente |
| Mínimo por código en `Utilidades` | .NET 6 (no usa nada posterior; no verificado compilando) |
| Mínimo por código en `DataBases` | .NET 8 (usa `ArgumentException.ThrowIfNullOrWhiteSpace`) |
| Nullable | Pensado para proyectos con `<Nullable>enable</Nullable>` |
| Consola | Para los símbolos `═ ╔ ║ •` hace falta una consola con soporte Unicode |

---

## Estructura de la solución

```text
Mis Librerias.slnx
├── Utilidades/
│   ├── Utilidades.csproj
│   ├── MyConsole.cs
│   ├── DecoracionConsola.cs
│   └── Funciones.cs
├── DataBases/
│   ├── DataBases.csproj
│   ├── CsvManager.cs
│   ├── ORM.cs
│   └── Atributos.cs
└── Documentacion/
```

---

## Compilación e instalación en una aplicación de consola

`Utilidades` y `DataBases` son proyectos de **biblioteca de clases**. No se instalan como paquetes: se agregan a la solución de la aplicación y se referencian desde su proyecto.

### Compilar las librerías solas

```bash
# Solución completa
dotnet build "Mis Librerias.slnx"

# Proyectos individuales
dotnet build Utilidades/Utilidades.csproj
dotnet build DataBases/DataBases.csproj
```

### Opción A: Visual Studio, con una aplicación nueva

1. Crear la aplicación: **Archivo > Nuevo > Proyecto > Aplicación de consola** (C#). Elegir como framework **.NET 8** o superior (ver "Notas" más abajo).
2. En el Explorador de soluciones, clic derecho sobre la solución de la aplicación > **Agregar > Proyecto existente...** y elegir `Utilidades/Utilidades.csproj`. Repetir con `DataBases/DataBases.csproj`.
3. Clic derecho sobre **Dependencias** del proyecto de consola > **Agregar referencia de proyecto...** y marcar `Utilidades`, `DataBases` o ambos.
4. Agregar los `using` en `Program.cs` (ver el ejemplo más abajo) y compilar.

Los nombres exactos de los menús pueden variar según la versión de Visual Studio.

### Opción B: línea de comandos, con una aplicación nueva

Los comandos suponen que las librerías están en `../Mis Librerias/`; ajustar la ruta.

```bash
dotnet new console -n MiApp
cd MiApp

# Solución propia de la aplicación, con los tres proyectos
dotnet new sln
dotnet sln add MiApp.csproj
dotnet sln add "../Mis Librerias/Utilidades/Utilidades.csproj"
dotnet sln add "../Mis Librerias/DataBases/DataBases.csproj"

# Referencias desde la aplicación a las librerías
dotnet add MiApp.csproj reference "../Mis Librerias/Utilidades/Utilidades.csproj"
dotnet add MiApp.csproj reference "../Mis Librerias/DataBases/DataBases.csproj"

dotnet run
```

### Opción C: agregar la aplicación a la solución de las librerías

Deja todo en una sola solución, útil mientras se desarrollan las librerías:

```bash
dotnet new console -n Consola
dotnet sln "Mis Librerias.slnx" add Consola/Consola.csproj
dotnet add Consola/Consola.csproj reference Utilidades/Utilidades.csproj DataBases/DataBases.csproj
```

En Visual Studio es equivalente a **Agregar > Nuevo proyecto** sobre la solución de las librerías y luego **Agregar referencia de proyecto...**.

### Programa de prueba

```csharp
using DataBases;
using Utilidades;
using static Utilidades.DecoracionConsola;

string nombre = Funciones.PedirString("Nombre: ");
ImprimirMesajeDeExito($"Hola {nombre}.");
```

Si solo se referencia `Utilidades`, el `using DataBases;` sobra (y al revés): las dos librerías son independientes.

### Notas

- **Versión de .NET.** Las dos librerías apuntan a `net8.0` (ver sus `.csproj`). Una aplicación solo puede referenciar proyectos que apunten a la misma versión o a una menor, así que la aplicación debe ser `net8.0` o superior (`net9.0`, `net10.0`, etc.). Para usar `Utilidades` desde una aplicación `net6.0` o `net7.0`, cambiar `<TargetFramework>` en `Utilidades.csproj`; `DataBases` requiere .NET 8 o superior.
- **No se copian los archivos.** La solución de la aplicación usa los proyectos originales: un cambio en las librerías afecta a todas las aplicaciones que las referencian.
- **Rutas.** La referencia de proyecto guarda una ruta relativa. Si se mueven carpetas, hay que volver a agregar las referencias.
- **Alternativa sin proyectos separados.** También se pueden copiar los `.cs` a la aplicación; en ese caso no hace falta ninguna referencia.

---

## Utilidades

### `Funciones`: entrada de datos y menús

Cada método repite la pregunta, mostrando un error en rojo, hasta que el dato es válido.

| Grupo | Métodos |
| :--- | :--- |
| Texto y lógicos | `PedirString`, `PedirChar`, `PedirBool` (acepta `y` / `n`) |
| Números | `PedirInt(mensaje, min, max)`, `PedirDouble(mensaje, min, max)` |
| Fechas y horas | `PedirDateOnly`, `PedirTimeOnly`, `PedirDateTime`, `PedirTimeSpan` |
| Contraseña | `LeerClave` (muestra `*` por cada carácter; `LeerNumeroClave` es el nombre anterior, obsoleto) |
| Enums | `PedirEnum<T>`, `PedirEnumFlags<T>` (para enums con `[Flags]`); las opciones salen en el orden de declaración |
| Menús con flechas | `PedirOpcionMenu`, `PedirOpcionMultipleMenu` |
| Pantalla | `WaitAndClean` (espera una tecla, limpia la pantalla y borra el historial) |

```csharp
using Utilidades;
using static Utilidades.DecoracionConsola;

string nombre = Funciones.PedirString("Nombre: ");
int edad = Funciones.PedirInt("Edad (18-99): ", 18, 99);
bool continuar = Funciones.PedirBool("¿Continuar? (y/n): ");

string[] opciones = { "Agregar", "Listar", "Salir" };
int elegido = Funciones.PedirOpcionMenu(opciones, "¿Qué desea hacer?");   // devuelve el índice

ImprimirMesajeDeExito($"Hola {nombre}.");
```

Aspectos a tener en cuenta:

- `PedirDouble` acepta `3,5` y `3.5`; rechaza `NaN`, infinito y separadores de miles.
- Fechas y horas se interpretan con la cultura del sistema.
- `PedirTimeSpan("5")` devuelve 5 **días** (comportamiento de .NET); conviene pedir el formato `hh:mm:ss`.
- Si `min > max` se lanza `ArgumentException`; lo mismo ocurre en `PedirInt` si entre `min` y `max` no hay ningún entero. Un menú `null` o vacío también lanza excepción.
- Si la entrada se cierra (Ctrl+Z / Ctrl+D o archivo redirigido agotado), se lanza `EndOfStreamException`.
- Con entrada o salida redirigidas, los menús pasan a un modo numerado en lugar de usar flechas.

### `DecoracionConsola`: formato de salida

| Grupo | Métodos |
| :--- | :--- |
| Mensajes de colores | `ImprimirMesajeDeExito`, `ImprimirMesajeDeError`, `ImprimirMesajeDeAdvertencia`, `ImprimirMesajeDeInformacion` |
| Progreso | `MostrarProgreso(ms)`, `MostrarBarraDeProgreso(ms)` (se adapta al ancho de la ventana) |
| Estructura | `ImprimirSeparador`, `ImprimirTextoConMarco`, `SaltarLineas`, `MostrarLista`, `MostrarTabla` |

```csharp
using static Utilidades.DecoracionConsola;

ImprimirTextoConMarco("Bienvenido");
// ╔══════════════╗
// ║  Bienvenido  ║
// ╚══════════════╝

string[,] tabla =
{
    { "ID", "Nombre" },
    { "1",  "Ana"    },
    { "2",  "Luis"   },
};
MostrarTabla(tabla);

MostrarBarraDeProgreso(2000);   // [#########-------] 56%
```

`MostrarTabla` y `ImprimirTextoConMarco` aceptan celdas `null`, saltos de línea y texto más ancho que la ventana (lo parten en varias líneas).

### `MyConsole`: envoltorio de `Console` con historial

Se usa igual que `Console` (`Write`, `WriteLine`, `ReadLine`, `ReadKey`, colores, cursor), pero puede **recordar lo escrito** para volver a dibujarlo después de limpiar la pantalla. Es lo que permite que los menús, que limpian la pantalla con cada tecla, conserven lo que había antes.

| Elemento | Función |
| :--- | :--- |
| `ActivarGuardado()` / `DesactivarGuardado()` / `GuardadoActivo` | Controla si se registra lo escrito. |
| `ReimprimirRegistros()` | Vuelve a dibujar lo registrado, en su posición y color originales. |
| `ReinicializarRegistros()` | Borra el historial. |
| `EsInteractiva` | `true` si ni la entrada ni la salida están redirigidas. |
| `AnchoSeguro` / `AltoSeguro` | Tamaño de la ventana; devuelven 80×24 si no se puede leer. |

Puntos importantes:

- Solo se registra lo escrito **con `MyConsole`**; `Console.WriteLine` directo no queda en el historial.
- El guardado está **activado desde el inicio**; los menús lo desactivan mientras dibujan y lo dejan activado al terminar.
- Las barras de progreso solo registran el resultado final, y `LeerClave` desactiva el guardado mientras se escribe la contraseña.
- El historial tiene un tope de 5000 entradas y es seguro entre hilos.
- Llamar a `Funciones.WaitAndClean()` al cambiar de pantalla evita que reaparezcan textos anteriores.

---

## DataBases

### `CsvManager`: persistencia en CSV

Guarda una `List<T>` en un archivo CSV y la reconstruye al leerla, sin escribir código distinto por cada clase.

| Método | Comportamiento |
| :--- | :--- |
| `Write<T>(ruta, lista)` | Reemplaza el archivo con toda la lista. Valida antes la clase (campos, tipos y constructor). Crea la carpeta si no existe. Escribe primero en un `.tmp` y luego lo mueve, para no dañar el archivo si se corta a mitad. |
| `Read<T>(ruta)` | Lee el archivo. Lanza `FileNotFoundException` si no existe. La clase se valida aunque el archivo esté vacío. |
| `ReadOrEmpty<T>(ruta)` | Igual que `Read`, pero devuelve una lista vacía si el archivo no existe. |

**Reglas para que una clase sea persistible:**

1. Cada dato a guardar es un **campo** (no una propiedad) marcado con `[Columna("nombre")]`.
2. Existe un **constructor** con un parámetro por cada campo marcado.
3. El nombre de cada parámetro coincide con el del campo (sin distinguir mayúsculas).
4. El tipo de cada parámetro es idéntico al del campo.

```csharp
using DataBases;

public class Persona
{
    [Atributos.Columna("id")]
    private int id;

    [Atributos.Columna("nombre")]
    private string nombre;

    [Atributos.Columna("email")]
    private string? email;          // puede ser null

    public Persona(int id, string nombre, string? email)
    {
        this.id = id;
        this.nombre = nombre;
        this.email = email;
    }

    public int Id => id;
    public string Nombre => nombre;
}

// Cargar (lista vacía si el archivo todavía no existe)
List<Persona> personas = CsvManager.ReadOrEmpty<Persona>("datos/personas.csv");

// Agregar y guardar la lista completa
personas.Add(new Persona(personas.Count + 1, "Ana", null));
CsvManager.Write("datos/personas.csv", personas);
```

Archivo resultante:

```text
id,nombre,email
1,"Ana",null
```

**Tipos soportados:** `string`, `char`, `bool`, todos los numéricos, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, cualquier `enum` (se guarda por nombre) y la versión `?` de todos ellos. No se soportan listas, arreglos, diccionarios ni objetos anidados: un campo de esos tipos lanza `CsvConfiguracionException`.

**Formato del archivo:** es un formato propio, inspirado en CSV pero **no idéntico al estándar** (RFC 4180):

- Separador siempre `,`; decimales siempre con punto (cultura invariante).
- Los textos van entre comillas; dentro de ellos `"` se escribe `\"`, `\` se escribe `\\` y los saltos de línea `\n`. Cada registro ocupa una sola línea.
- Un valor `null` se escribe como la palabra `null` sin comillas; `""` es el texto vacío y `"null"` (con comillas) es el texto `null`. Al leer, una celda vacía sin comillas también se interpreta como `null`.
- El orden de las columnas al leer no importa; se asocian por nombre, y deben estar todas.
- Los campos de clases base también se guardan (primero los de la base).

**Errores propios:**

| Excepción | Significado |
| :--- | :--- |
| `CsvException` | Base de las dos siguientes. |
| `CsvFormatException` | El **archivo** tiene un problema. Incluye `Linea` y `Columna`. |
| `CsvConfiguracionException` | La **clase** está mal definida (sin `[Columna]`, nombres repetidos, tipo de campo no soportado, constructor incompatible). |

Los errores de entrada y salida (`IOException`, `UnauthorizedAccessException`) no se envuelven.

> No se recomienda guardar los CSV desde Excel: puede cambiar el separador a `;`, reescribir las comillas y alterar las fechas. Tampoco es apto para datos sensibles: el archivo es texto plano.

### `Atributos`

Clases anidadas dentro de la clase estática `Atributos`. Para escribirlas sin el prefijo: `using static DataBases.Atributos;`.

| Atributo | Se aplica a | Argumentos |
| :--- | :--- | :--- |
| `Columna` | Campos | `nombre` |
| `Relacion_1_1` | Propiedades | `campoOrigen`, `campoDestino` |
| `Relacion_1_N` | Propiedades | `campoOrigen`, `campoDestino` |
| `Relacion_1_1_Tabla` | Propiedades | `tipoTabla`, `campoOrigen`, `campoTablaOrigen`, `campoTablaDestino`, `campoDestino` |
| `Relacion_1_N_Tabla` | Propiedades | `tipoTabla`, `campoOrigen`, `campoTablaOrigen`, `campoTablaDestino`, `campoDestino` |

Los atributos solo guardan los textos; las validaciones se hacen al resolver la relación.

### `ORM`: relaciones entre objetos en memoria

`CsvManager` guarda solo la clave foránea (por ejemplo `clienteId`). El `ORM` busca, entre tablas **ya cargadas**, el objeto relacionado. No lee ni escribe archivos ni modifica tablas.

| Método | Relación | Tabla intermedia | Devuelve |
| :--- | :--- | :--- | :--- |
| `Relacionar_1_1<T>` | 1 → 1 | No | `T?` |
| `Relacionar_1_N<T>` | 1 → N | No | `IReadOnlyList<T>` |
| `Relacionar_1_1_Tabla<T>` | 1 → 1 | Sí | `T?` |
| `Relacionar_1_N_Tabla<T>` | 1 → N | Sí | `IReadOnlyList<T>` |

La relación se declara con un atributo **sobre una propiedad** y se resuelve al acceder a ella (lazy). Los nombres del atributo son **campos**, no propiedades, y distinguen mayúsculas.

```csharp
using DataBases;
using static DataBases.Atributos;

public static class Datos
{
    public static List<Cliente> Clientes = new();
    public static List<Venta> Ventas = new();
}

public class Cliente
{
    [Columna("id")]
    private int id;

    [Columna("nombre")]
    private string nombre;

    public Cliente(int id, string nombre)
    {
        this.id = id;
        this.nombre = nombre;
    }

    public string Nombre => nombre;

    [Relacion_1_N("id", "clienteId")]
    public IReadOnlyList<Venta> Ventas =>
        ORM.Relacionar_1_N(this, nameof(Ventas), Datos.Ventas);
}

public class Venta
{
    [Columna("id")]
    private int id;

    [Columna("clienteId")]
    private int clienteId;

    private Cliente? cliente;       // sin [Columna]: no se guarda

    public Venta(int id, int clienteId)
    {
        this.id = id;
        this.clienteId = clienteId;
    }

    [Relacion_1_1("clienteId", "id")]
    public Cliente? Cliente =>
        cliente ??= ORM.Relacionar_1_1(this, nameof(Cliente), Datos.Clientes);
}
```

```csharp
// Cargar TODAS las tablas antes de usar relaciones
Datos.Clientes = CsvManager.ReadOrEmpty<Cliente>("datos/clientes.csv");
Datos.Ventas   = CsvManager.ReadOrEmpty<Venta>("datos/ventas.csv");

foreach (Cliente c in Datos.Clientes)
    Console.WriteLine($"{c.Nombre}: {c.Ventas.Count} ventas");
```

**Qué devuelve según los datos:**

| Situación | `_1_1` / `_1_1_Tabla` | `_1_N` / `_1_N_Tabla` |
| :--- | :--- | :--- |
| Clave del objeto actual `null` | `null` | lista vacía |
| Ninguna coincidencia | `null` | lista vacía |
| Una coincidencia | el objeto | lista de 1 |
| Varias coincidencias | `InvalidOperationException` | lista de N |

Otros comportamientos:

- Una clave foránea opcional (`int?`) es compatible con la clave destino (`int`). Un `null` nunca relaciona objetos.
- Las relaciones con tabla intermedia no producen duplicados por filas repetidas, y devuelven los resultados en el orden de la tabla destino.
- Los errores de configuración o de datos se lanzan como `InvalidOperationException`, con un mensaje que indica qué atributo, campo o dato está mal.
- El resultado de `??=` queda guardado en la propiedad: si cambia la clave o la tabla, hay que asignar `null` al campo para forzar una nueva búsqueda.

---

## Limitaciones principales

| Área | Limitación |
| :--- | :--- |
| `CsvManager` | El `lock` protege solo dentro de un mismo proceso y es único para todos los archivos. |
| `CsvManager` | `Write` guarda siempre la lista completa; no hay transacciones entre varios archivos. |
| `CsvManager` | Renombrar o agregar columnas a una clase deja inservibles los archivos existentes. |
| `CsvManager` | Lee el archivo completo en memoria. |
| `ORM` | Sin índices: cada acceso recorre las tablas completas. |
| `ORM` | Las tablas no están sincronizadas; no deben modificarse mientras se resuelve una relación. |
| `ORM` | Las claves deben ser **campos** y la tabla intermedia debe ser de un tipo **clase**. |
| `MyConsole` | Redimensionar la ventana puede desordenar lo redibujado (usa coordenadas absolutas). |
| `MyConsole` | Los emoji y caracteres de ancho doble desalinean marcos y tablas. |
| General | Los menús y las entradas asumen un solo hilo interactuando con el usuario. |

---

## Documentación detallada

Cada componente tiene una guía completa (conceptos, manual, manejo de errores, funcionamiento interno, decisiones de diseño y preguntas frecuentes) en la carpeta [`Documentacion/`](Documentacion/):

- [CsvManager - Documentación completa](Documentacion/CsvManager%20-%20Documentaci%C3%B3n%20completa.md)
- [ORM - Documentación completa](Documentacion/ORM%20-%20Documentaci%C3%B3n%20completa.md)
- [Utilidades - Documentación completa](Documentacion/Utilidades%20-%20Documentaci%C3%B3n%20completa.md)
