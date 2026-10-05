# Utilidades — Documentación completa

Guía para usar, entender y mantener la librería `Utilidades` (archivos `Funciones.cs`, `DecoracionConsola.cs` y `MyConsole.cs`): el conjunto de clases que facilita **pedir datos por consola con validación**, **mostrar menús navegables con las flechas** y **dar formato a lo que se imprime** (mensajes de colores, barras de progreso, marcos y tablas).

Está escrita para que la pueda leer cualquier persona, incluso sin experiencia previa con consolas ni con C#. Cada sección es independiente: se puede saltar directamente a la que interese.

> **Estado de verificación:** este código y esta documentación no fueron compilados ni ejecutados por quien los escribió. Antes de usarlos en algo importante, compile el proyecto y pruebe los casos descritos en este documento.

---

## Índice

1. [¿Qué es Utilidades?](#1-qu%C3%A9-es-utilidades)
2. [Conceptos necesarios](#2-conceptos-necesarios)
3. [Requisitos e instalación](#3-requisitos-e-instalaci%C3%B3n)
4. [Manual de uso](#4-manual-de-uso)
5. [Controles y convenciones de la interfaz](#5-controles-y-convenciones-de-la-interfaz)
6. [Manejo de errores](#6-manejo-de-errores)
7. [Funcionamiento interno](#7-funcionamiento-interno)
8. [Decisiones de diseño](#8-decisiones-de-dise%C3%B1o)
9. [Limitaciones conocidas](#9-limitaciones-conocidas)
10. [Posibles dudas](#10-posibles-dudas)

---

# 1. ¿Qué es Utilidades?

Un programa de consola necesita hacer siempre las mismas tareas: preguntar un número y repetir la pregunta si el usuario escribe una letra, mostrar un menú, imprimir un error en rojo. Escribir ese código en cada programa es repetitivo y propenso a errores.

La librería `Utilidades` reúne esas tareas en tres clases, una por archivo:

| Clase | Para qué sirve |
| --- | --- |
| `Funciones` | **Pedir datos** al usuario (texto, números, fechas, contraseñas, enums) y **menús** con las flechas del teclado. |
| `DecoracionConsola` | **Mostrar** cosas con formato: mensajes de colores, barras de progreso, marcos, listas y tablas. |
| `MyConsole` | Un envoltorio de `Console` que además puede **recordar lo escrito** para volver a dibujarlo después de limpiar la pantalla. |

## Ejemplo mínimo

```csharp
using Utilidades;
using static Utilidades.DecoracionConsola;

string nombre = Funciones.PedirString("Nombre: ");
int edad = Funciones.PedirInt("Edad: ", 0, 120);   // repite hasta que sea un entero entre 0 y 120

ImprimirMesajeDeExito($"Hola {nombre}, tienes {edad} años.");

bool seguir = Funciones.PedirBool("¿Continuar? (y/n): ");
```

Si el usuario escribe `abc` en la edad, el programa muestra `ERROR: numero invalido` en rojo y vuelve a preguntar, sin que haga falta escribir ninguna validación.

## Qué incluye

| Grupo | Elementos |
| --- | --- |
| **Entradas** | `PedirString`, `PedirBool`, `PedirInt`, `PedirDouble`, `PedirChar`, `LeerClave` (`LeerNumeroClave` queda como nombre obsoleto) |
| **Fechas y horas** | `PedirDateOnly`, `PedirTimeOnly`, `PedirDateTime`, `PedirTimeSpan` |
| **Enums** | `PedirEnum`, `PedirEnumFlags` *(nuevo)* |
| **Menús** | `PedirOpcionMenu`, `PedirOpcionMultipleMenu` |
| **Mensajes** | `ImprimirMesajeDeExito`, `...DeError`, `...DeAdvertencia`, `...DeInformacion` |
| **Presentación** | `MostrarProgreso`, `MostrarBarraDeProgreso`, `ImprimirSeparador`, `ImprimirTextoConMarco`, `SaltarLineas`, `MostrarLista`, `MostrarTabla` |
| **Utilidad** | `WaitAndClean` |
| **Consola** | `MyConsole` (Write, WriteLine, ReadLine, ReadKey, colores, cursor, historial) |

---

# 2. Conceptos necesarios

Esta sección explica, desde cero, las ideas que aparecen en el resto del documento. Quien ya las conozca puede saltarla.

## 2.1 La consola

La *consola* es la ventana de texto donde un programa muestra información y recibe datos del teclado. En C# se maneja con la clase `Console`.

| Acción | Instrucción |
| --- | --- |
| Escribir sin salto de línea | `Console.Write("texto")` |
| Escribir con salto de línea | `Console.WriteLine("texto")` |
| Leer una línea (hasta Enter) | `Console.ReadLine()` |
| Leer **una tecla** | `Console.ReadKey()` |

`ReadLine` espera a que se pulse Enter. `ReadKey` reacciona a **cada tecla** al instante: es lo que permite los menús con flechas.

## 2.2 Coordenadas, ventana y buffer

El texto de la consola se ubica en una cuadrícula de **columnas** (X, horizontal) y **filas** (Y, vertical). La esquina superior izquierda es `(0, 0)`.

```text
 X →   0 1 2 3 4 ...
 Y
 ↓ 0   H o l a
   1   M e n ú
   2   ...
```

- **Ventana:** la parte visible (`WindowWidth` × `WindowHeight`).
- **Buffer:** toda la memoria de texto de la consola (`BufferWidth` × `BufferHeight`), que puede ser más grande que la ventana.
- **Cursor:** el punto donde se escribirá el próximo carácter (`CursorLeft`, `CursorTop`). Se puede mover con `SetCursorPosition(x, y)`.

Si se intenta mover el cursor fuera del buffer, .NET lanza `ArgumentOutOfRangeException`.

## 2.3 Secuencias especiales: `\r`, `\n`, `\b`

| Secuencia | Significado |
| --- | --- |
| `\n` | Salto de línea. |
| `\r` | **Retorno de carro:** vuelve el cursor al inicio de la línea **sin** bajar. Permite reescribir sobre lo mismo (así funciona la barra de progreso). |
| `\b` | Retrocede un carácter. |
| `\t` | Tabulación (ancho variable, por eso desalinea tablas). |

## 2.4 Colores

`ConsoleColor` es una lista de colores predefinidos (`Red`, `Green`, `DarkGreen`, `Cyan`, ...). Se cambia el color del texto con `ForegroundColor` y el del fondo con `BackgroundColor`. El color **queda cambiado** hasta que se vuelva a modificar, por eso las funciones lo guardan y lo restauran.

## 2.5 Entrada y salida redirigidas

Un programa puede ejecutarse **sin** una ventana de consola real, por ejemplo:

```text
programa.exe < datos.txt          (la entrada viene de un archivo)
programa.exe > salida.txt         (la salida va a un archivo)
```

En esos casos no hay teclado ni pantalla "en vivo". Operaciones como `ReadKey`, `Clear` o leer el cursor **fallan** o no tienen sentido. `Console.IsInputRedirected` y `Console.IsOutputRedirected` indican si ocurre esto.

## 2.6 `Parse` y `TryParse`

Convertir un texto (`"42"`) en un número (`42`) puede fallar si el texto no es un número.

| Método | Si el texto es inválido |
| --- | --- |
| `int.Parse("abc")` | **Lanza una excepción**. |
| `int.TryParse("abc", out int n)` | Devuelve `false`, sin excepción. |

`Utilidades` usa siempre `TryParse` y vuelve a preguntar si devuelve `false`.

## 2.7 Cultura

Cada región escribe números y fechas distinto:

| Región | Número | Fecha |
| --- | --- | --- |
| Argentina (es-AR) | `3,5` | `01/10/2026` (día/mes/año) |
| Estados Unidos (en-US) | `3.5` | `10/01/2026` (mes/día/año) |

- **Cultura actual** (`CultureInfo.CurrentCulture`): la configuración de la computadora del usuario.
- **Cultura invariante** (`CultureInfo.InvariantCulture`): un formato fijo, igual en todas partes (punto decimal).

Para lo que **escribe una persona** se usa la cultura actual. Para guardar en archivos se usa la invariante (como hace `CsvManager`).

## 2.8 `NaN` e infinito

Los `double` admiten valores especiales: `NaN` ("no es un número") e `Infinity`. `double.TryParse("NaN")` devuelve `true`. Además, `NaN` **no es menor ni mayor que nada**: `NaN < 5` y `NaN > 5` son ambos `false`, por lo que un control de rango como "si es menor que el mínimo o mayor que el máximo, rechazar" **no lo detecta**.

## 2.9 Parámetros opcionales

```csharp
public static int PedirInt(string mensaje, double min = int.MinValue, double max = int.MaxValue)
```

`min` y `max` son opcionales: si no se pasan, valen lo indicado. Se pueden llamar así:

```csharp
PedirInt("Edad: ");              // sin límites
PedirInt("Edad: ", 0);           // solo mínimo
PedirInt("Edad: ", 0, 120);      // ambos
```

## 2.10 Excepciones y `try / finally`

Una *excepción* es un objeto que describe un error. Si nadie la atrapa, el programa se detiene. `try/finally` garantiza que cierto código se ejecute **siempre**, ocurra o no un error:

```csharp
try
{
    // código que puede fallar
}
finally
{
    // se ejecuta SIEMPRE: ideal para restaurar colores o estados
}
```

## 2.11 Genéricos con restricciones

```csharp
public static TEnum PedirEnum<TEnum>(string mensaje) where TEnum : struct, Enum
```

`TEnum` es un tipo que se elige al usar el método. `where TEnum : struct, Enum` obliga a que sea un **enum**. Ejemplo: `PedirEnum<Estado>("Estado: ")`.

## 2.12 Enums y `[Flags]`

Un *enum* es una lista de nombres con valores (`Activo`, `Inactivo`). Con el atributo `[Flags]` un enum puede representar **combinaciones** (por ejemplo, permisos `Lectura | Escritura`).

## 2.13 Hilos y `lock`

Un programa puede ejecutar tareas en paralelo (*hilos*). Si dos hilos modifican la misma lista a la vez, puede fallar. Un `lock` deja pasar a un hilo por vez.

---

# 3. Requisitos e instalación

## 3.1 Requisitos

| Requisito | Detalle |
| --- | --- |
| **.NET** | El proyecto `Utilidades.csproj` apunta a `net8.0`, por lo que la aplicación debe ser `net8.0` o superior. Por su código, la librería no usa nada posterior a .NET 6 (`DateOnly`, `TimeOnly`, `ArgumentNullException.ThrowIfNull`); para usarla en .NET 6 o 7 se cambia `<TargetFramework>` en el `.csproj`. Esto último no fue verificado compilando. |
| **Nullable** | Pensado para proyectos con `<Nullable>enable</Nullable>`. |
| **Consola** | Funciona mejor en una consola real. Sin ella, usa modos alternativos (sección 5.4). |
| **Fuente** | Algunos símbolos (`═ ╔ ║ •`) requieren una consola con soporte Unicode. |

## 3.2 Instalación

La forma recomendada es agregar el proyecto como **referencia de proyecto** desde la aplicación de consola (pasos para Visual Studio y para la línea de comandos en el README, sección "Compilación e instalación en una aplicación de consola"). La alternativa es copiar los archivos, como se describe a continuación.

1. Si se copian los archivos: copiar la carpeta con los archivos al proyecto (`Funciones.cs`, `DecoracionConsola.cs` y `MyConsole.cs`, todos con `namespace Utilidades`). Si el proyecto tenía un `Utilidades.cs` único con las tres clases, eliminarlo: de lo contrario el compilador marcará nombres duplicados. Con la referencia de proyecto, omitir este paso.
2. Agregar el `using`:

```csharp
using Utilidades;
using static Utilidades.DecoracionConsola;   // para llamar ImprimirMesajeDeError(...) directamente
```

3. Compilar. Los nombres públicos son los de la versión original, pero hay cambios de comportamiento que conviene conocer:
   - `PedirDouble` ya no limita por defecto el rango a los valores de `int` (ver 4.2).
   - `MyConsole.ReadLine` lanza `EndOfStreamException` cuando la entrada se cierra (ver 7.4).
   - `WaitAndClean` borra el historial y no muestra la tecla pulsada (ver 4.10).
   - El historial de `MyConsole` empieza **activado** (ver 4.10).
   - `LeerNumeroClave` pasó a llamarse `LeerClave`; el nombre anterior sigue funcionando pero está marcado como obsoleto.
   - Los mensajes de error de fecha, hora y duración empiezan con minúscula (`fecha invalida`).

---

# 4. Manual de uso

## 4.1 Entradas simples

### `PedirString`

```csharp
string nombre = Funciones.PedirString("Nombre: ");
```

Muestra el mensaje y devuelve lo que el usuario escribe. Nunca devuelve `null` (si no escribe nada, devuelve `""`).

### `PedirBool`

```csharp
bool acepta = Funciones.PedirBool("¿Desea continuar? (y/n): ");
```

Acepta `y` o `n` (mayúsculas o minúsculas, con o sin espacios alrededor). Cualquier otra cosa muestra un error y vuelve a preguntar.

### `PedirChar`

```csharp
char letra = Funciones.PedirChar("Letra: ");
```

Exige **exactamente un carácter**.

## 4.2 Números

### `PedirInt` y `PedirDouble`

```csharp
int edad        = Funciones.PedirInt("Edad: ", 0, 120);
double precio   = Funciones.PedirDouble("Precio: ", 0);
double cualquiera = Funciones.PedirDouble("Valor: ");   // sin límites
```

| Aspecto | Comportamiento |
| --- | --- |
| **Texto inválido** | `ERROR: numero invalido` y vuelve a preguntar. |
| **Fuera de rango** | `ERROR: numero fuera de rango (permitido: min a max)` y vuelve a preguntar. |
| **Límites** | `min` y `max` incluidos. |
| **Límites por defecto de `PedirDouble`** | Sin límites (`double.MinValue` a `double.MaxValue`). La versión original rechazaba valores fuera de ±2 147 483 647. |
| **Límites inconsistentes** | Si `min > max`, lanza `ArgumentException` de inmediato (error del programador). |
| **Rango sin enteros (`PedirInt`)** | Los límites decimales se redondean hacia adentro (`0.2` → 1, `9.8` → 9). Si no queda ningún entero (por ejemplo, entre `0.2` y `0.8`), lanza `ArgumentException`. |
| **Mensaje de rango (`PedirDouble`)** | Si un límite no se indicó, el mensaje dice `desde X` o `hasta X` en lugar de mostrar los valores extremos de `double`. |
| **Decimales (`PedirDouble`)** | Acepta tanto el formato regional (`3,5` en es-AR) como el invariante (`3.5`). |
| **Separador de miles** | **No** se acepta (`1.234` no es "mil doscientos treinta y cuatro"). |
| **`NaN` / `Infinity`** | Se rechazan como inválidos. |
| **Notación científica** | `PedirDouble` acepta `1e5`. |

> **Nota:** `PedirInt` recibe los límites como `double` (heredado de la versión original). Así se puede escribir `PedirInt("x", 0.5, 10)`, aunque lo habitual es pasar enteros.

## 4.3 Contraseñas

```csharp
MyConsole.Write("Clave: ");
string clave = Funciones.LeerClave();
```

- Muestra un `*` por cada carácter y **no** muestra lo escrito.
- `Backspace` borra el último carácter.
- Ignora flechas, Tab, Esc y otras teclas especiales.
- Termina con Enter y devuelve el texto escrito.
- Si la entrada está redirigida, lee una línea normal (sin ocultar).
- Mientras dura la lectura, el historial de `MyConsole` se desactiva, de modo que la contraseña no queda registrada ni se vuelve a dibujar. Al terminar se restaura el estado anterior.
- `LeerNumeroClave` es el nombre anterior de este método. Sigue funcionando, pero está marcado como `[Obsolete]`.

## 4.4 Fechas y horas

```csharp
DateOnly fecha     = Funciones.PedirDateOnly("Fecha: ");
TimeOnly hora      = Funciones.PedirTimeOnly("Hora: ");
DateTime momento   = Funciones.PedirDateTime("Fecha: ", "Hora: ");
TimeSpan duracion  = Funciones.PedirTimeSpan("Duración (hh:mm:ss): ");
```

Se interpretan con la **cultura de la computadora**: en Argentina, `01/10/2026` es el 1 de octubre.

> **Cuidado con `PedirTimeSpan`:** `TimeSpan.TryParse("5")` interpreta `5` como **5 días**, no como 5 segundos. Conviene indicar el formato en el mensaje (`hh:mm:ss`).

## 4.5 Enums

```csharp
public enum Estado { Activo, Inactivo, Suspendido }

Estado estado = Funciones.PedirEnum<Estado>("Estado: ");     // menú con flechas
```

Las opciones del menú aparecen en el orden en que se declararon en el enum.

### Enums con `[Flags]` (combinables)

```csharp
[Flags]
public enum Permisos { Ninguno = 0, Lectura = 1, Escritura = 2, Borrado = 4 }

Permisos p = Funciones.PedirEnumFlags<Permisos>("Permisos: ");   // se marcan varios con Espacio
```

| Situación | Resultado |
| --- | --- |
| Enum sin valores | `InvalidOperationException` |
| `PedirEnumFlags` con un enum **sin** `[Flags]` | `InvalidOperationException` (usar `PedirEnum`) |
| `PedirEnumFlags` sin marcar nada | Devuelve el valor `0` (`default`) |

## 4.6 Menús

### Menú de selección única

```csharp
string[] opciones = { "Agregar", "Listar", "Salir" };

int elegido = Funciones.PedirOpcionMenu(opciones, "¿Qué desea hacer?");

if (elegido == 0) { /* Agregar */ }
```

Devuelve el **índice** (empezando en 0) de la opción elegida.

### Menú de selección múltiple

```csharp
List<int> marcados = Funciones.PedirOpcionMultipleMenu(opciones, "Elija los que quiera:");

foreach (int i in marcados)
    Console.WriteLine(opciones[i]);
```

Devuelve los índices marcados **ordenados de menor a mayor**. Puede devolver una lista vacía.

### Colores personalizados

```csharp
Funciones.PedirOpcionMenu(opciones, "Menú:",
    seleccion:   ConsoleColor.Yellow,
    cursor:      ConsoleColor.Cyan,
    noSeleccion: ConsoleColor.Gray,
    textoNormal: ConsoleColor.White);
```

### Menús largos

Si el menú tiene más opciones de las que caben en la ventana, se muestran **solo algunas** con los avisos `... mas opciones arriba` y `... mas opciones abajo`, y la lista se desplaza con el cursor.

### Menús y validaciones

| Situación | Resultado |
| --- | --- |
| `menu` es `null` | `ArgumentNullException` |
| `menu` está vacío | `ArgumentException` |
| Opción `null` dentro del menú | Se muestra como línea vacía |
| Opción muy larga | Se recorta con `...` para que ocupe una línea |

## 4.7 Mensajes de colores

```csharp
ImprimirMesajeDeExito("Datos guardados.");        // EXITO: Datos guardados.        (verde)
ImprimirMesajeDeError("Archivo no encontrado.");  // ERROR: Archivo no encontrado.  (rojo)
ImprimirMesajeDeAdvertencia("Disco casi lleno."); // ADVERTENCIA: ...               (amarillo)
ImprimirMesajeDeInformacion("Cargando...");       // INFO: ...                      (cian)
```

El color original se restaura siempre después de imprimir. Un mensaje `null` se imprime vacío.

## 4.8 Progreso

```csharp
MostrarProgreso(2000);            // Progreso: 0% ... 100%   (dura unos 2000 ms)
MostrarBarraDeProgreso(2000);     // [#########-------] 56%  (se adapta al ancho de la ventana)
```

El parámetro es el tiempo total en **milisegundos**.

Los cuadros intermedios de la animación no se registran en el historial de `MyConsole`: solo queda registrado el resultado final (100%). Así, cada llamada ocupa un par de entradas y no 101.

## 4.9 Separadores, marcos, listas y tablas

```csharp
ImprimirSeparador();          // línea de ═ del ancho de la ventana
ImprimirSeparador('-');       // con otro carácter
SaltarLineas(2);              // dos líneas en blanco

ImprimirTextoConMarco("Bienvenido");
// ╔══════════════╗
// ║  Bienvenido  ║   (el marco se ajusta al texto)
// ╚══════════════╝

MostrarLista(new[] { "Manzana", "Pera" });
//   • Manzana
//   • Pera

string[,] tabla =
{
    { "ID", "Nombre", "Nota"   },
    { "1",  "Ana",    "9"      },
    { "2",  "Luis",   "Aprobó\ncon observaciones" },
};
MostrarTabla(tabla);
// +----+--------+-------------------+
// | ID | Nombre | Nota              |
// +----+--------+-------------------+
// | 1  | Ana    | 9                 |
// +----+--------+-------------------+
// | 2  | Luis   | Aprobó            |
// |    |        | con observaciones |   (las celdas pueden tener varias líneas)
// +----+--------+-------------------+
```

| Elemento | Comportamiento con casos especiales |
| --- | --- |
| `ImprimirTextoConMarco` | Texto `null` → marco vacío. Saltos de línea → varias filas dentro del marco. Texto más ancho que la ventana → se parte en varias líneas. |
| `MostrarLista` | Arreglo `null` → `ArgumentNullException`. Elemento `null` → línea vacía. |
| `MostrarTabla` | Matriz `null` → `ArgumentNullException`. Matriz sin filas o columnas → no imprime nada. Celda `null` → vacía. Más ancha que la ventana → se reduce la columna más ancha y se parte su texto. |

## 4.10 Historial de pantalla: `MyConsole`

`MyConsole` se usa igual que `Console` (`Write`, `WriteLine`, `ReadLine`, `ReadKey`, colores, cursor), pero puede **recordar** lo que se escribió.

```csharp
MyConsole.ActivarGuardado();
MyConsole.WriteLine("Esto queda registrado");
...
MyConsole.Clear();
MyConsole.ReimprimirRegistros();    // vuelve a dibujar lo registrado
```

Para qué sirve: los menús **limpian la pantalla** para redibujarse con cada tecla; el historial permite volver a mostrar lo que había **antes** del menú.

| Método | Función |
| --- | --- |
| `ActivarGuardado()` / `DesactivarGuardado()` | Activa o desactiva el registro. |
| `GuardadoActivo` | Indica si está activo. |
| `ReimprimirRegistros()` | Vuelve a dibujar lo registrado, en su posición y color originales. |
| `ReinicializarRegistros()` | Borra el historial. |
| `EsInteractiva` | `true` si hay teclado y pantalla reales. |
| `AnchoSeguro` / `AltoSeguro` | Tamaño de la ventana; nunca fallan (devuelven 80×24 si no se puede leer). |

> **Importante:** el guardado está **activado desde el inicio**, por lo que todo lo que se imprima con `MyConsole` queda registrado, incluso antes del primer menú. Los menús lo desactivan mientras dibujan los cuadros intermedios y lo dejan **activado** al terminar. Se puede desactivar a mano con `DesactivarGuardado()`.

Con la salida redirigida, `ReimprimirRegistros` no hace nada, porque no hay pantalla donde reposicionar el cursor.

### `WaitAndClean`

```csharp
Funciones.WaitAndClean();
```

Espera a que se pulse una tecla (sin mostrarla en pantalla), limpia la pantalla **y borra el historial**, de modo que la nueva pantalla empiece vacía. Con la entrada redirigida no espera ninguna tecla.

## 4.11 Ejemplo completo

```csharp
using Utilidades;
using static Utilidades.DecoracionConsola;

enum Opcion { Sumar, Restar, Salir }

class Programa
{
    static void Main()
    {
        while (true)
        {
            Opcion op = Funciones.PedirEnum<Opcion>("Calculadora");

            if (op == Opcion.Salir)
                break;

            double a = Funciones.PedirDouble("Primer número: ");
            double b = Funciones.PedirDouble("Segundo número: ");

            double resultado = op == Opcion.Sumar ? a + b : a - b;

            ImprimirTextoConMarco($"Resultado: {resultado}");
            Funciones.WaitAndClean();
        }
    }
}
```

## 4.12 Buenas prácticas

- **Usar `MyConsole` en vez de `Console`** si se quiere que el historial funcione. Lo escrito con `Console.WriteLine` directamente **no se registra**.
- **Llamar a `WaitAndClean`** al cambiar de pantalla, para no arrastrar el historial anterior.
- **Capturar `EndOfStreamException`** si el programa puede ejecutarse con entrada redirigida.
- **Indicar el formato esperado en el mensaje** de fechas y duraciones (`"Fecha (dd/mm/aaaa): "`).
- **No imprimir con `Console` mientras un menú está activo.**
- **No redimensionar la ventana** durante un menú (ver 9).

---

# 5. Controles y convenciones de la interfaz

## 5.1 Teclas de los menús

| Tecla | Menú simple | Menú múltiple |
| --- | --- | --- |
| `↑` / `↓` | Mover el cursor | Mover el cursor |
| `Home` / `End` | Ir a la primera / última opción | Ídem |
| `Re Pág` / `Av Pág` | Subir / bajar una "página" | Ídem |
| `Enter` | **Elegir** la opción | **Terminar** la selección |
| `Espacio` | *(sin efecto)* | **Marcar / desmarcar** |

Los extremos no "dan la vuelta": estando en la primera opción, `↑` no hace nada.

## 5.2 Colores por defecto de los menús

| Elemento | Color por defecto |
| --- | --- |
| Opción bajo el cursor | Verde (`Green`) |
| Opciones sin seleccionar | Verde oscuro (`DarkGreen`) |
| Opción elegida / marcada | Blanco (`White`) |
| Texto del mensaje | Verde oscuro (`DarkGreen`) |

El cursor se señala además con `>` delante de la opción.

## 5.3 Prefijos y colores de los mensajes

| Función | Prefijo | Color |
| --- | --- | --- |
| `ImprimirMesajeDeExito` | `EXITO:` | Verde |
| `ImprimirMesajeDeError` | `ERROR:` | Rojo |
| `ImprimirMesajeDeAdvertencia` | `ADVERTENCIA:` | Amarillo |
| `ImprimirMesajeDeInformacion` | `INFO:` | Cian |

## 5.4 Si no hay consola interactiva

Cuando la entrada o la salida están redirigidas, los menús no pueden usar flechas. En su lugar:

```text
¿Qué desea hacer?
  1) Agregar
  2) Listar
  3) Salir
Opcion (numero): _
```

- **Menú simple:** se pide el número de la opción.
- **Menú múltiple:** se piden números separados por coma (`1,3`); vacío = ninguna.
- `LeerClave` lee una línea normal (sin ocultar).
- `WaitAndClean` no espera ninguna tecla.
- `Clear` no hace nada.

---

# 6. Manejo de errores

## 6.1 Dos clases de problemas

| Tipo | Ejemplo | Qué ocurre |
| --- | --- | --- |
| **El usuario escribe algo inválido** | `abc` donde va un número | **No** hay excepción: se muestra `ERROR: ...` y se vuelve a preguntar. |
| **El programador usa mal una función** | Un menú vacío, `min > max` | Se **lanza una excepción** inmediatamente. |

## 6.2 Excepciones que pueden lanzarse

| Excepción | Cuándo |
| --- | --- |
| `ArgumentException` | `min > max` o límite `NaN` en `PedirInt` / `PedirDouble`; ningún entero en el rango de `PedirInt`; menú vacío. |
| `ArgumentNullException` | Menú `null`; arreglo `null` en `MostrarLista`; matriz `null` en `MostrarTabla`. |
| `InvalidOperationException` | Enum sin valores; `PedirEnumFlags` con un enum sin `[Flags]`. |
| `EndOfStreamException` | La entrada se cerró (fin de archivo, Ctrl+Z / Ctrl+D) mientras se esperaba un dato. |
| `ArgumentOutOfRangeException` | Uso directo de `MyConsole.SetCursorPosition` con coordenadas fuera del buffer. |

## 6.3 Mensajes que ve el usuario

| Mensaje | Función | Causa |
| --- | --- | --- |
| `ERROR: opcion no valida (escriba y o n)` | `PedirBool` | Respuesta distinta de `y` / `n`. |
| `ERROR: numero invalido` | `PedirInt`, `PedirDouble` | El texto no es un número (o es `NaN` / infinito). |
| `ERROR: numero fuera de rango (permitido: min a max)` | `PedirInt`, `PedirDouble` | Número fuera de `[min, max]`. |
| `ERROR: char invalido (se espera exactamente un caracter)` | `PedirChar` | Vacío o más de un carácter. |
| `ERROR: fecha invalida` | `PedirDateOnly`, `PedirDateTime` | No se pudo interpretar como fecha. |
| `ERROR: hora invalida` | `PedirTimeOnly`, `PedirDateTime` | No se pudo interpretar como hora. |
| `ERROR: duracion invalida` | `PedirTimeSpan` | No se pudo interpretar como duración. |
| `ERROR: opcion no valida` | Menú sin teclado | Números fuera de rango o mal escritos. |

## 6.4 Patrón recomendado

```csharp
try
{
    int opcion = Funciones.PedirOpcionMenu(opciones, "Menú:");
}
catch (EndOfStreamException)
{
    // La entrada se cerró: terminar el programa de forma ordenada.
    return;
}
catch (ArgumentException ex)
{
    // Error de programación: revisar los argumentos.
    Console.Error.WriteLine(ex.Message);
}
```

> `ArgumentNullException` hereda de `ArgumentException`, por lo que este `catch` atrapa ambas.

## 6.5 Cómo diagnosticar

```text
¿Qué ocurrió?
│
├─ Se repite una pregunta con ERROR rojo
│     → El dato escrito no es válido. Revisar el formato esperado.
│
├─ ArgumentException al llamar a PedirInt/PedirDouble
│     → min > max, o un límite es NaN.
│
├─ ArgumentException / ArgumentNullException en un menú
│     → La lista de opciones está vacía o es null.
│
├─ EndOfStreamException
│     → La entrada estándar se cerró (archivo redirigido agotado, Ctrl+Z).
│
└─ La pantalla se ve desordenada o se repiten textos antiguos
      → Revisar el historial (sección 7.2): usar WaitAndClean al cambiar de pantalla,
        no redimensionar la ventana, o llamar a ReinicializarRegistros().
```

---

# 7. Funcionamiento interno

## 7.1 Mapa de la biblioteca

```text
Utilidades
│
├── Funciones
│    ├── Entradas:  PedirString, PedirBool, PedirChar, PedirInt, PedirDouble,
│    │              PedirDateOnly, PedirTimeOnly, PedirDateTime, PedirTimeSpan,
│    │              LeerClave, WaitAndClean
│    ├── Enums:     PedirEnum, PedirEnumFlags
│    ├── Menús:     PedirOpcionMenu ──┐
│    │              PedirOpcionMultipleMenu ─┴─▶ EjecutarMenu (núcleo común)
│    │                                              └─▶ EjecutarMenuSimple (sin teclado)
│    └── Privados:  ValidarRango, DescribirRango, TryParseDouble,
│                   LeerClaveInteractiva, BorrarUltimoAsterisco,
│                   NombresEnOrdenDeclaracion, ImprimirIndicador, Recortar
│
├── DecoracionConsola
│    ├── Mensajes:  ImprimirMesajeDe{Exito,Error,Advertencia,Informacion} ─▶ ImprimirConColor
│    ├── Progreso:  MostrarProgreso, MostrarBarraDeProgreso
│    ├── Formato:   ImprimirSeparador, ImprimirTextoConMarco, SaltarLineas,
│    │              MostrarLista, MostrarTabla
│    └── Privados:  NormalizarTexto, PartirLinea, EjecutarSinRegistrar
│
└── MyConsole
     ├── Escritura/lectura: Write, WriteLine, ReadLine, ReadKey
     ├── Historial:         registros, Registrar, ReimprimirRegistros, ReinicializarRegistros
     └── Seguridad:         LeerPosicion, AnchoSeguro, AltoSeguro, EsInteractiva
```

Todas las funciones de `Funciones` y `DecoracionConsola` escriben y leen **a través de `MyConsole`**. Eso permite que el historial funcione sin que cada función tenga que ocuparse de él.

## 7.2 El historial de pantalla (MyConsole)

### Problema que resuelve

Un menú se redibuja con **cada tecla**: limpia la pantalla y dibuja de nuevo las opciones con el cursor en otra posición. Pero al limpiar, también se borra lo que estaba **antes** del menú (la pregunta anterior, los datos ya mostrados). El historial permite volver a dibujar eso.

### Cómo funciona

Cada vez que se escribe con `MyConsole` y el guardado está activo, se guarda una `Linea`:

```text
Linea { Texto, ColorTexto, ColorFondo, X, Y }
```

`X` e `Y` son la posición donde empezó a escribirse el texto.

```text
 Escribir:                       Redibujar (ReimprimirRegistros):
 ─────────────────────           ────────────────────────────────
 WriteLine("Hola")  ──┐          Para cada Linea guardada:
   guarda posición    │             1. Mover el cursor a (X, Y)
   escribe en pantalla│             2. Poner sus colores
   registra la Linea  ▼             3. Escribir su Texto
 registros = [ Linea1, Linea2, ... ]
```

### Qué se registra y qué no

| Operación | ¿Se registra? |
| --- | --- |
| `MyConsole.Write` / `WriteLine` | Sí (si el guardado está activo) |
| `MyConsole.ReadLine` | Sí: se guarda lo escrito por el usuario, en la posición donde empezó |
| `ReimprimirRegistros` | No (escribe con `Console` directamente) |
| Cuadros intermedios de las barras de progreso | No (solo el resultado final) |
| `LeerClave` | No (el guardado se desactiva mientras se lee) |
| `Console.WriteLine` directo | **No** |

### Protecciones agregadas

| Protección | Motivo |
| --- | --- |
| **Tope de 5000 entradas** | Sin tope, la lista crecía sin límite durante toda la ejecución. |
| **`lock`** | Evita errores si varios hilos escriben a la vez. |
| **Copia antes de redibujar** | Redibujar recorre una copia; así otro hilo puede seguir escribiendo. |
| **Restaurar colores originales** | Antes se llamaba a `ResetColor`, que borraba los colores personalizados. |
| **Omitir posiciones inválidas** | Si la ventana se achicó, el texto cuya posición ya no existe se omite en lugar de abortar. |
| **Salida redirigida** | `ReimprimirRegistros` no hace nada. |

## 7.3 Seguridad ante consolas especiales

`MyConsole` evita que el programa falle cuando no hay una consola "real":

| Elemento | Qué hace si la consola no lo permite |
| --- | --- |
| `CursorLeft` / `CursorTop` | Devuelven `0`. |
| `Clear()` | No hace nada. |
| `AnchoSeguro` / `AltoSeguro` | Devuelven `80` / `24`. |
| `EsInteractiva` | `true` solo si **ni** la entrada **ni** la salida están redirigidas. |
| `WindowWidth` / `WindowHeight` | Devuelven lo mismo que `AnchoSeguro` / `AltoSeguro` (80 / 24 si no se puede leer). |
| `BufferWidth` / `BufferHeight` | Si no se pueden leer, devuelven el tamaño de la ventana. |
| `CursorVisible` | Si la plataforma no lo soporta, leerlo devuelve `true` y asignarlo no hace nada. |
| `SetCursorPosition` | Sin consola real no hace nada. Una coordenada fuera del buffer sigue lanzando `ArgumentOutOfRangeException`. |

## 7.4 `ReadLine` y el fin de la entrada

`Console.ReadLine()` devuelve `null` cuando la entrada se cierra (fin de un archivo redirigido, Ctrl+Z en Windows, Ctrl+D en Linux). La versión original lo transformaba en `""`, lo que hacía que `PedirInt` y similares quedaran en un **bucle infinito**: preguntaban, recibían vacío, mostraban "numero invalido" y volvían a preguntar para siempre.

Ahora `MyConsole.ReadLine` lanza `EndOfStreamException`, que corta el bucle.

## 7.5 Cómo funcionan `PedirInt` y `PedirDouble`

```text
1. ValidarRango(min, max)         → error inmediato si min > max o hay NaN
2. Solo PedirInt: llevar los límites al rango real de int y redondearlos
   hacia adentro (Ceiling del mínimo, Floor del máximo).
   Si no queda ningún entero → ArgumentException.
3. Repetir:
     a. texto = PedirString(mensaje)
     b. ¿Se puede convertir a número (y, en double, es finito)?
          NO → "numero invalido" → volver a (a)
     c. ¿Está dentro de [min, max]?
          NO → "numero fuera de rango (...)" → volver a (a)
     d. devolver el número
```

### Interpretación de decimales (`TryParseDouble`)

```text
1. Probar con la cultura actual           (en es-AR: "3,5" → 3.5)
2. Si falla, probar con la cultura invariante   ("3.5" → 3.5)
```

Se usa `NumberStyles.Float`, que **excluye el separador de miles**. Sin esa exclusión, `1.234` en es-AR se leería como `1234` y en en-US como `1.234`: el mismo texto daría números muy distintos.

## 7.6 `LeerClave`

```text
Desactivar el guardado de MyConsole (se restaura al terminar, aunque haya error).
Si la entrada está redirigida → leer una línea normal.
Si no, repetir hasta Enter:
  · Backspace → si hay caracteres: quitar el último y borrar el * de pantalla
  · Tecla "imprimible" (no es de control) → agregarla y mostrar *
  · Cualquier otra (flechas, Tab, Esc, F1...) → ignorar
```

### Borrado de un asterisco

```text
¿Columna del cursor > 0?
   SÍ → retroceder una columna
   NO → ¿Hay una fila arriba?
          SÍ → ir a la última columna de la fila anterior
          NO → no hacer nada
Escribir un espacio en esa posición y volver a ella.
```

Esto resuelve el caso de una contraseña larga cuyo texto ya pasó a la línea siguiente.

## 7.7 El motor de menús (`EjecutarMenu`)

Los dos menús comparten un mismo núcleo. Un parámetro `multiple` determina las diferencias.

### Bucle principal

```text
Mientras no sea el último cuadro:
   1. Limpiar la pantalla
   2. Redibujar el historial (lo que había antes del menú)
   3. Dibujar el mensaje y la ayuda de teclas
   4. Calcular cuántas opciones caben (ventana de desplazamiento)
   5. Dibujar las opciones visibles con sus colores
   6. Si no es el último cuadro:
         leer una tecla y actuar (mover, marcar, o confirmar)
Dibujar un último cuadro con la selección final.
```

### ¿Por qué hay un "último cuadro"?

Al pulsar Enter, no se sale de inmediato: se hace **una vuelta más** del bucle que dibuja el menú en su estado final (la opción elegida en el color de selección) y **sin esperar tecla**. Así el resultado queda visible en pantalla.

### Guardado durante el menú

```text
Al entrar:  anotar si el guardado estaba activo → DESACTIVAR el guardado
            (los cuadros intermedios NO deben quedar en el historial)
Último cuadro: ACTIVAR el guardado → el cuadro final SÍ queda registrado
Al salir con error: volver al estado de guardado anterior
```

### Ventana de desplazamiento

```text
filasLibres = alto de la ventana − fila actual del cursor − 1
¿Caben todas las opciones?
   SÍ → mostrar todas
   NO → mostrar (filasLibres − 2) y reservar 2 líneas para los avisos
        "... mas opciones arriba" / "... mas opciones abajo"

El "desplazamiento" (primera opción visible) se ajusta para que el cursor
siempre quede dentro de la parte visible.
```

Cada opción se recorta a una sola línea; si ocupara dos, el menú crecería y se desplazaría toda la pantalla.

### Sin teclado (`EjecutarMenuSimple`)

Si la entrada o la salida están redirigidas, se imprime la lista numerada y se pide el número con `PedirInt`; en el menú múltiple se interpretan números separados por coma.

## 7.8 `MostrarTabla`

```text
1. Normalizar cada celda: null → "", \r\n → \n, \t → 4 espacios.
   Partirla en líneas.
2. Calcular el ancho de cada columna = su línea más larga.
3. ¿La suma de anchos supera lo que cabe en la ventana?
     SÍ → reducir de a 1 carácter la columna MÁS ancha, repetir hasta que quepa.
          (mínimo 1 por columna)
4. Partir las líneas que superen el ancho de su columna.
5. Dibujar, fila por fila. La altura de una fila = la celda con más líneas.
```

Espacio ocupado por la tabla: cada columna aporta su ancho **más** 2 espacios (margen) **más** 1 borde `|`, y hay 1 borde final.

## 7.9 `ImprimirTextoConMarco`

```text
1. Normalizar el texto (null, saltos de línea, tabulaciones).
2. Ancho máximo de línea = ancho de ventana − 1 − 6 (los 6 caracteres del marco).
3. Partir el texto en líneas de ese ancho como máximo.
4. Ancho del marco = línea más larga + 4.
5. Dibujar: borde superior, cada línea rellena con espacios, borde inferior.
```

## 7.10 Barra de progreso adaptable

```text
ancho de la barra = (ancho de ventana − 8), limitado entre 10 y 100
Para i de 0 a 100:
    llenos = i × ancho / 100
    imprimir "\r[" + "#"×llenos + "-"×(ancho−llenos) + "] " + i + "%"
```

Los cuadros intermedios se escriben con el guardado desactivado (`EjecutarSinRegistrar`); al terminar se escribe una vez más el cuadro al 100% con el guardado restaurado, para que quede registrado.

El `\r` hace que cada cuadro **reescriba sobre la misma línea**. Si la barra fuera más ancha que la ventana, el texto saltaría de línea y cada cuadro quedaría debajo del anterior.

---

# 8. Decisiones de diseño

Estas elecciones son razonables pero discutibles. Se documentan para poder cambiarlas con conocimiento.

| Decisión | Alternativa | Motivo de la elección |
| --- | --- | --- |
| El historial **nace activado** y los menús lo dejan activado al terminar | Nacer desactivado | Todo lo escrito con `MyConsole` se puede redibujar desde el principio. Se puede desactivar con `DesactivarGuardado()`. |
| `EndOfStreamException` al cerrarse la entrada | Seguir devolviendo `""` | Evita bucles infinitos. |
| Aceptar `3,5` y `3.5` | Solo la cultura actual | Más tolerante con el usuario. |
| Rechazar el separador de miles | Aceptarlo | Evita ambigüedad entre `1.234` y `1,234`. |
| Errores del programador → excepción; errores del usuario → reintentar | Todo reintentar | Un menú vacío o `min > max` no lo arregla el usuario. |
| Opciones largas se recortan con `...` | Dejar que se envuelvan | Mantener cada opción en una línea para que el menú no se desplace. |
| Tablas: reducir la columna más ancha | Truncar con `...` | No se pierde información: el texto se parte. |
| Historial: coordenadas absolutas | Guardar solo el texto y reproducirlo en orden | Las coordenadas permiten reescribir sobre la misma posición (barras de progreso, asteriscos borrados). |
| Un único método `EjecutarMenu` | Dos bucles separados | Menos duplicación. |
| Tope de 5000 registros | Sin tope | Equilibrio entre memoria y fidelidad del redibujado. |
| Ordenar la lista del menú múltiple | Orden de marcado | Resultado predecible. |
| Cuadros de las barras de progreso sin registrar | Registrarlos todos | Cada animación llenaría el historial con 101 entradas. |
| `LeerClave` desactiva el historial | Dejarlo como esté | La contraseña no queda en memoria ni se redibuja. |
| `PedirInt` ajusta el rango a enteros | Aceptar rangos decimales sin revisar | Evita un bucle infinito cuando ningún entero cabe en el rango. |
| Opciones de enum en orden de declaración | Orden de `Enum.GetNames` (por valor) | Coincide con lo que el programador escribió. |
| `LeerNumeroClave` se conserva como obsoleto | Eliminarlo | No rompe programas existentes; el nombre nuevo describe mejor lo que hace. |

---

# 9. Limitaciones conocidas

| Limitación | Detalle |
| --- | --- |
| **Redimensionar la ventana** | El historial usa coordenadas absolutas; si la ventana cambia de tamaño, lo redibujado puede quedar desordenado. Solución: `ReinicializarRegistros()` o evitar redimensionar. |
| **Desplazamiento de la consola** | Si el texto llega al final del buffer y la consola se desplaza, las coordenadas guardadas dejan de coincidir. |
| **Caracteres anchos** | Emoji o caracteres de ancho doble ocupan 2 columnas pero cuentan como 1: marcos y tablas pueden desalinearse. |
| **Lo escrito con `Console` directo** | No se guarda en el historial. |
| **Hilos** | El historial es seguro entre hilos, pero un menú no debe usarse desde varios hilos a la vez. |
| **`TimeSpan.TryParse("5")`** | Se interpreta como 5 días (comportamiento de .NET). |
| **`CursorVisible`** | En algunos sistemas (macOS/Linux) leer esta propiedad no está soportado. |
| **Unicode** | `═ ╔ ║ •` pueden verse mal en consolas sin soporte. |
| **Entrada de texto sin edición avanzada** | `LeerClave` no maneja las teclas Suprimir ni las flechas laterales. |
| **Menú sin "ciclo"** | En los extremos la flecha no salta al lado contrario. |
| **Orden de los enums** | Se obtiene por Reflection (`GetFields`); en la práctica sigue el orden de declaración, aunque .NET no lo garantiza formalmente. |

---

# 10. Posibles dudas

### 10.1 ¿Por qué mi texto desaparece al mostrar un menú?

Porque el menú limpia la pantalla. Solo se vuelve a dibujar lo escrito **con `MyConsole`** y con el guardado activo. Si imprimió con `Console.WriteLine`, no está en el historial. Reemplace por `MyConsole.WriteLine`.

### 10.2 ¿Por qué al cambiar de pantalla reaparecen cosas viejas?

El historial sigue guardando lo anterior. Llame a `Funciones.WaitAndClean()` o a `MyConsole.ReinicializarRegistros()` al cambiar de pantalla.

### 10.3 ¿Por qué `PedirDouble` acepta `3,5` y `3.5`?

Primero se prueba con el formato regional y, si falla, con el invariante. Así el usuario no necesita saber qué separador espera el programa.

### 10.4 ¿Por qué `1.234` no se interpreta como mil doscientos treinta y cuatro?

Porque se excluyó el separador de miles a propósito: el mismo texto significaría cosas distintas según la región. Se interpreta como 1,234.

### 10.5 ¿Cómo pido un `int` sin límites?

`Funciones.PedirInt("Número: ")`. Los límites por defecto son los del propio `int`.

### 10.6 ¿Por qué `PedirTimeSpan` interpreta `5` como 5 días?

Es el comportamiento de .NET. Pida el formato `hh:mm:ss` en el mensaje.

### 10.7 ¿Cómo hago que el usuario elija varios valores de un enum?

Si el enum tiene `[Flags]`, use `PedirEnumFlags<T>`. Si no, use `PedirOpcionMultipleMenu(Enum.GetNames<T>(), ...)` y convierta cada índice.

### 10.8 ¿Qué pasa si el usuario cierra la entrada con Ctrl+Z / Ctrl+D?

Se lanza `EndOfStreamException`. Si no se captura, el programa termina con ese error. Capturela para cerrar de forma ordenada.

### 10.9 ¿Por qué el menú dice "mas opciones abajo"?

El menú tiene más opciones de las que caben en la ventana. Use las flechas para desplazarse, o agrande la ventana antes de abrirlo.

### 10.10 ¿Puedo usar los menús con la salida redirigida?

Sí: pasan a un modo numerado (sección 5.4).

### 10.11 ¿Por qué mis opciones aparecen cortadas con `...`?

Una opción más ancha que la ventana se recorta para que ocupe una línea. Acorte el texto o agrande la ventana.

### 10.12 ¿El guardado está activado por defecto?

Sí. `MyConsole` empieza con el guardado activado y los menús lo dejan activado al terminar. Se puede desactivar con `MyConsole.DesactivarGuardado()`; mientras esté desactivado, lo que se escriba no se vuelve a dibujar después de un menú.

### 10.13 ¿Por qué hay una vuelta extra al pulsar Enter en un menú?

Para dibujar el estado final (la opción elegida resaltada) sin esperar otra tecla (sección 7.7).

### 10.14 ¿`PedirString` puede devolver `null`?

No. Devuelve `""` si no se escribe nada. Lo que sí puede ocurrir es `EndOfStreamException` si la entrada se cierra.

### 10.15 ¿Por qué se ve mal el marco con emoji?

Los emoji ocupan dos columnas pero cuentan como un carácter, y el cálculo de anchos no lo considera (sección 9).

### 10.16 ¿Es seguro para uso con varios hilos?

El historial de `MyConsole` sí. Pero los menús y las entradas asumen que **un solo hilo** interactúa con el usuario a la vez.

### 10.17 ¿Cómo cambio los colores de todos los menús?

Pase los parámetros de color en cada llamada, o cree un método propio que llame a `PedirOpcionMenu` con sus colores preferidos.

### 10.18 ¿Por qué `LeerNumeroClave` aparece como obsoleto?

Se renombró a `LeerClave` porque lee una contraseña, no un número. El nombre anterior sigue funcionando (llama a `LeerClave`), pero el compilador muestra una advertencia. Reemplazar las llamadas por `Funciones.LeerClave()`.

---

*Fin del documento.*
