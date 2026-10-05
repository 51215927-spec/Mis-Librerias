# ORM — Documentación completa

Guía para usar, entender y mantener `ORM`, la clase que permite relacionar objetos de distintas tablas cargadas en memoria.

Está escrita para que la pueda leer cualquier persona, incluso sin experiencia previa con Reflection, atributos u ORM. Cada sección es independiente: se puede saltar directamente a la que interese.

> **Estado de verificación:** este código y esta documentación no fueron compilados ni ejecutados por quien los escribió. Antes de usarlos en algo importante, compile el proyecto y pruebe los casos descritos en este documento.

---

# Índice

1. [¿Qué es ORM?](#1-qué-es-orm)
2. [Conceptos necesarios](#2-conceptos-necesarios)
3. [Requisitos e instalación](#3-requisitos-e-instalación)
4. [Manual de uso](#4-manual-de-uso)
5. [Tipos de relaciones](#5-tipos-de-relaciones)
6. [Manejo de errores](#6-manejo-de-errores)
7. [Funcionamiento interno](#7-funcionamiento-interno)
8. [Decisiones de diseño](#8-decisiones-de-diseño)
9. [Limitaciones conocidas](#9-limitaciones-conocidas)
10. [Posibles dudas](#10-posibles-dudas)

---

# 1. ¿Qué es ORM?

**ORM** significa *Object-Relational Mapping* ("mapeo objeto-relacional").

En este proyecto, `ORM` se encarga de **relacionar objetos que pertenecen a tablas diferentes**.

Por ejemplo, una `Venta` puede tener un `clienteId` que indica qué `Cliente` realizó la compra:

```text
Venta
┌─────────────┐
│ id = 15     │
│ clienteId=3 │ ──────────┐
└─────────────┘           │
                          ▼
                    Cliente
                    ┌───────────┐
                    │ id = 3    │
                    │ nombre=Ana│
                    └───────────┘
```

El CSV solamente guarda:

```text
id,clienteId
15,3
```

No guarda el objeto `Cliente` completo dentro de `Venta`.

El ORM utiliza el `clienteId` para buscar el objeto correspondiente en la tabla de clientes.

## Ejemplo mínimo

```csharp
public class Cliente
{
    [Atributos.Columna("id")]
    private int id;

    [Atributos.Columna("nombre")]
    private string nombre;

    public Cliente(int id, string nombre)
    {
        this.id = id;
        this.nombre = nombre;
    }
}
```

```csharp
public class Venta
{
    [Atributos.Columna("id")]
    private int id;

    [Atributos.Columna("clienteId")]
    private int clienteId;

    private Cliente? cliente;

    [Atributos.Relacion_1_1("clienteId", "id")]
    public Cliente? Cliente =>
        cliente ??= ORM.Relacionar_1_1(
            this,
            nameof(Cliente),
            Datos.Clientes);
}
```

Cuando se ejecuta:

```csharp
Venta venta = ...;

Cliente? cliente = venta.Cliente;
```

el ORM:

1. Obtiene el `clienteId` de la venta.
2. Busca un `Cliente` cuyo `id` coincida.
3. Devuelve ese objeto.
4. La propiedad lo guarda en `cliente` para reutilizarlo.

## Qué incluye

| Elemento                      | Función                                                       |
| ----------------------------- | ------------------------------------------------------------- |
| `ORM.Relacionar_1_1<T>`       | Relaciona un objeto con un único objeto de otra tabla.        |
| `ORM.Relacionar_1_N<T>`       | Relaciona un objeto con varios objetos de otra tabla.         |
| `ORM.Relacionar_1_1_Tabla<T>` | Relaciona un objeto con uno mediante una tabla intermedia.    |
| `ORM.Relacionar_1_N_Tabla<T>` | Relaciona un objeto con varios mediante una tabla intermedia. |
| `Relacion_1_1Attribute`       | Describe una relación 1 a 1 directa.                          |
| `Relacion_1_NAttribute`       | Describe una relación 1 a N directa.                          |
| `Relacion_1_1_TablaAttribute` | Describe una relación 1 a 1 mediante tabla intermedia.        |
| `Relacion_1_N_TablaAttribute` | Describe una relación 1 a N mediante tabla intermedia.        |

Los cuatro atributos están declarados como clases anidadas dentro de la clase estática `Atributos`, no en `ORM`. `Atributos` pertenece al namespace `DataBases`.

Los cuatro atributos se aplican **solo a propiedades** (`AttributeUsage(AttributeTargets.Property)`). Colocarlos sobre un campo o una clase produce un error de compilación.

| Atributo                      | Argumentos del constructor, en orden                                              | Propiedades de solo lectura                                                       |
| ----------------------------- | --------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `Relacion_1_1Attribute`       | `campoOrigen`, `campoDestino`                                                     | `CampoOrigen`, `CampoDestino`                                                     |
| `Relacion_1_NAttribute`       | `campoOrigen`, `campoDestino`                                                     | `CampoOrigen`, `CampoDestino`                                                     |
| `Relacion_1_1_TablaAttribute` | `tipoTabla`, `campoOrigen`, `campoTablaOrigen`, `campoTablaDestino`, `campoDestino` | `TipoTabla`, `CampoOrigen`, `CampoTablaOrigen`, `CampoTablaDestino`, `CampoDestino` |
| `Relacion_1_N_TablaAttribute` | `tipoTabla`, `campoOrigen`, `campoTablaOrigen`, `campoTablaDestino`, `campoDestino` | `TipoTabla`, `CampoOrigen`, `CampoTablaOrigen`, `CampoTablaDestino`, `CampoDestino` |

Los atributos solo guardan los textos que reciben; no los validan. Las validaciones (campo inexistente, tipos incompatibles, `TipoTabla` en `null`) las hace el ORM al resolver la relación.

## Qué no incluye

El ORM **no** lee ni escribe archivos, **no** modifica tablas y **no** crea ni elimina objetos relacionados. Solo busca, entre objetos que ya están en memoria, los que cumplen una relación declarada con atributos.

---

# 2. Conceptos necesarios

## 2.1 Tabla

En este proyecto, una tabla es una colección de objetos de una misma clase.

Por ejemplo:

```csharp
List<Cliente> clientes;
List<Venta> ventas;
```

representan dos tablas:

```text
Tabla Clientes
┌────┬──────┐
│ id │nombre│
├────┼──────┤
│ 1  │ Ana  │
│ 2  │ Luis │
└────┴──────┘

Tabla Ventas
┌────┬─────────┐
│ id │clienteId│
├────┼─────────┤
│ 10 │    1    │
│ 11 │    2    │
└────┴─────────┘
```

Normalmente estas tablas son las listas obtenidas mediante `CsvManager.Read` o `ReadOrEmpty`. El ORM acepta cualquier `IEnumerable<T>`, no solamente `List<T>`.

---

## 2.2 Clave primaria

Una **clave primaria** es un valor que identifica un objeto de forma única dentro de una tabla.

Por ejemplo:

```csharp
private int id;
```

con valores:

```text
Cliente 1 → id = 1
Cliente 2 → id = 2
Cliente 3 → id = 3
```

El ORM utiliza estos identificadores para encontrar los objetos relacionados.

---

## 2.3 Clave foránea

Una **clave foránea** es un campo que contiene el identificador de otro objeto.

Por ejemplo:

```csharp
private int clienteId;
```

Si:

```text
clienteId = 3
```

significa:

```text
Esta venta pertenece al Cliente cuyo id es 3.
```

El ORM no necesita guardar el objeto `Cliente` en el CSV.

Guarda solamente:

```text
clienteId = 3
```

y reconstruye la relación en memoria.

Una clave foránea puede ser opcional (`int?`). Si vale `null`, significa "sin relación" (ver 4.6).

---

## 2.4 Relación

Una relación indica que un objeto está conectado con otro objeto u objetos.

Ejemplo:

```text
Venta ────────► Cliente
```

Una venta pertenece a un cliente.

También puede ocurrir:

```text
Cliente ────────► Ventas
```

Un cliente puede tener muchas ventas.

---

## 2.5 Relación 1 a 1

Una relación **1 a 1** significa que un objeto se relaciona con como máximo un objeto de la otra tabla.

Ejemplo:

```text
Persona ───────► Pasaporte
```

Una persona tiene un pasaporte.

---

## 2.6 Relación 1 a N

Una relación **1 a N** significa que un objeto puede relacionarse con varios objetos.

Ejemplo:

```text
Cliente
   │
   ├── Venta 1
   ├── Venta 2
   └── Venta 3
```

Un cliente puede tener muchas ventas.

---

## 2.7 Tabla intermedia

Algunas relaciones necesitan una tercera tabla.

Por ejemplo:

```text
Alumno
   │
   ▼
AlumnoMateria
   │
   ▼
Materia
```

La tabla intermedia podría ser:

```text
AlumnoMateria
┌────────┬─────────┐
│alumnoId│materiaId│
├────────┼─────────┤
│   1    │    5    │
│   1    │    8    │
│   2    │    5    │
└────────┴─────────┘
```

Esta tabla indica qué alumno está relacionado con qué materia.

Es especialmente útil para relaciones donde ninguno de los dos objetos puede guardar directamente el identificador del otro.

---

## 2.8 Atributos

El ORM utiliza atributos para describir **cómo debe buscar una relación**.

Por ejemplo:

```csharp
[Atributos.Relacion_1_1("clienteId", "id")]
public Cliente? Cliente => ...;
```

Esto significa:

```text
Campo del objeto actual: clienteId
Campo del objeto destino: id
```

Por lo tanto:

```text
Venta.clienteId == Cliente.id
```

---

## 2.9 Reflection

El ORM no conoce de antemano las clases que va a relacionar.

Por eso utiliza **Reflection** para descubrir:

* qué propiedad contiene la relación;
* qué atributo tiene;
* qué campos debe comparar;
* qué tipos tienen esos campos;
* qué tipo tiene una tabla intermedia.

Por ejemplo:

```csharp
PropertyInfo propiedad =
    objeto.GetType().GetProperty(nombrePropiedad);
```

permite encontrar una propiedad durante la ejecución.

Y:

```csharp
FieldInfo campo =
    tipo.GetField(...);
```

permite encontrar un campo.

---

## 2.10 Relación lazy

El ORM está diseñado para trabajar de forma **lazy** (*diferida*).

Eso significa que la relación no se busca inmediatamente cuando se crea el objeto.

Por ejemplo:

```csharp
Venta venta = ...
```

no busca todavía al cliente.

La búsqueda ocurre cuando se accede a:

```csharp
venta.Cliente
```

Esto evita cargar relaciones que quizás nunca sean utilizadas.

---

## 2.11 Caché de Reflection

Buscar una propiedad, un atributo o un campo con Reflection cuesta tiempo. Como una relación se resuelve cada vez que se accede a su propiedad (a veces dentro de un bucle), el ORM **recuerda** lo que ya encontró:

```text
Primera vez:   buscar propiedad → buscar atributo → buscar campos → guardar
Siguientes:    leer lo guardado
```

Solo se guardan las búsquedas **exitosas**. Un error de configuración no se guarda: se vuelve a lanzar en cada llamada.

Este caché es interno del ORM y es distinto del "caché lazy" que el programador puede escribir en su propiedad con `??=` (ver 2.12 y 4.8).

---

## 2.12 Caché del objeto relacionado (`??=`)

El operador `??=` significa "si la variable es `null`, calcular y guardar; si no, usar lo que ya tiene":

```csharp
cliente ??= ORM.Relacionar_1_1(...);
```

Lo escribe el programador en su propiedad. El ORM no lo hace por su cuenta.

---

## 2.13 Genéricos con restricción `where T : class`

```csharp
public static T? Relacionar_1_1<T>(...) where T : class
```

`T` es el tipo de los objetos de la tabla destino. La restricción `where T : class` obliga a que sea una **clase** (no un `struct`). Por eso `T?` significa "un objeto o `null`".

En las llamadas habituales no hace falta escribir `T`: se deduce de la tabla.

```csharp
ORM.Relacionar_1_1(this, nameof(Cliente), Datos.Clientes);   // T = Cliente
```

---

## 2.14 `IEnumerable<object>` y covarianza

Las tablas intermedias se reciben como `IEnumerable<object>`. Una `List<AlumnoMateria>` se puede pasar directamente, porque `IEnumerable<T>` es **covariante**: una secuencia de `AlumnoMateria` también es una secuencia de `object`.

Esto solo funciona si el tipo de la tabla intermedia es una **clase**. Con un `struct` el compilador rechaza la llamada.

---

## 2.15 Datos externos y errores del programador

Existen dos orígenes de error distintos:

| Origen                          | Ejemplo                                               | Quién lo corrige   |
| ------------------------------- | ----------------------------------------------------- | ------------------ |
| **Configuración** (el código)   | Un atributo que nombra un campo que no existe.        | El programador.    |
| **Datos** (las tablas)          | Dos clientes con el mismo `id` en una relación 1 a 1. | Quien cargó datos. |

El ORM lanza `InvalidOperationException` en ambos casos, con un mensaje que indica cuál es el problema.

---

# 3. Requisitos e instalación

## 3.1 Requisitos

| Requisito           | Detalle                                                                         |
| ------------------- | ------------------------------------------------------------------------------- |
| **.NET**            | .NET 8 o superior (`DataBases.csproj` apunta a `net8.0`).                       |
| **C#**              | Versión compatible con el proyecto.                                             |
| **Nullable**        | Pensado para proyectos con `<Nullable>enable</Nullable>`.                       |
| **Reflection**      | Forma parte de .NET.                                                            |
| **DataBases**       | Debe estar disponible el namespace del proyecto.                                |
| **Tablas cargadas** | Las listas relacionadas deben haber sido cargadas antes de resolver relaciones. |

---

## 3.2 Instalación

El ORM forma parte de la biblioteca `DataBases`.

Agregar:

```csharp
using DataBases;
```

Los atributos se escriben con el prefijo `Atributos.`:

```csharp
[Atributos.Relacion_1_1("clienteId", "id")]
```

Para usarlos sin el prefijo:

```csharp
using static DataBases.Atributos;
```

---

## 3.3 Orden de carga

Las tablas deben cargarse antes de utilizar relaciones:

```csharp
Datos.Clientes =
    CsvManager.ReadOrEmpty<Cliente>("datos/clientes.csv");

Datos.Ventas =
    CsvManager.ReadOrEmpty<Venta>("datos/ventas.csv");
```

Después pueden utilizarse:

```csharp
venta.Cliente
```

El ORM trabaja con objetos que ya están en memoria. No abre ni lee los CSV por su cuenta.

---

# 4. Manual de uso

## 4.1 Preparar una relación

Para crear una relación hay tres elementos:

1. Un campo que identifica la relación.
2. Una propiedad que expone el objeto relacionado. Puede ser pública, privada, protegida o interna.
3. Un atributo, **sobre esa propiedad**, que describe cómo encontrarlo.

Ejemplo:

```csharp
private int clienteId;

private Cliente? cliente;

[Relacion_1_1("clienteId", "id")]
public Cliente? Cliente =>
    cliente ??= ORM.Relacionar_1_1(
        this,
        nameof(Cliente),
        Datos.Clientes);
```

El campo:

```csharp
private int clienteId;
```

se guarda en el CSV.

El campo:

```csharp
private Cliente? cliente;
```

no lleva `[Columna]`, por lo que `CsvManager` no lo guarda.

La propiedad:

```csharp
public Cliente? Cliente
```

es la que permite acceder a la relación.

### Reglas

| # | Regla                                                                                                    | Por qué                                                             |
| - | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| 1 | La propiedad debe ser de **instancia**. Puede tener cualquier visibilidad (pública, privada, protegida o interna). | Se buscan propiedades de instancia, sin importar su visibilidad. |
| 2 | La propiedad debe tener el atributo que corresponde al método que se llama.                              | El atributo contiene los nombres de los campos.                     |
| 3 | Los nombres del atributo son **campos**, no propiedades.                                                 | El ORM lee campos con Reflection.                                   |
| 4 | Los nombres de campo respetan **mayúsculas y minúsculas** (`"id"` no es `"Id"`).                         | La búsqueda distingue mayúsculas.                                   |
| 5 | Los dos campos que se comparan tienen el **mismo tipo** (se permite `int` con `int?`).                   | Comparar tipos distintos nunca representaría una relación correcta. |
| 6 | Se pasa `nameof(Propiedad)` y no un texto escrito a mano.                                                | Si la propiedad cambia de nombre, el compilador lo detecta.         |
| 7 | El atributo se coloca sobre la **propiedad**, nunca sobre un campo.                                      | Los atributos de relación solo admiten propiedades como destino.    |

### Visibilidad de la propiedad

La propiedad que lleva el atributo puede ser privada. El ORM no la invoca: solo lee su atributo, de modo que su visibilidad no afecta la resolución.

```csharp
private Cliente? cliente;

[Relacion_1_1("clienteId", "id")]
private Cliente? Cliente =>
    cliente ??= ORM.Relacionar_1_1(this, nameof(Cliente), Datos.Clientes);

public string NombreCliente => Cliente?.Nombre ?? "(sin cliente)";
```

Una propiedad privada solo se puede usar dentro de la propia clase. Si el resto del programa necesita el dato, se expone con un miembro público (como `NombreCliente` en el ejemplo).

---

## 4.2 Relación 1 a 1 directa

Se utiliza:

```csharp
ORM.Relacionar_1_1<T>(objeto, nombrePropiedad, tabla)
```

Ejemplo:

```csharp
[Relacion_1_1("clienteId", "id")]
public Cliente? Cliente =>
    cliente ??= ORM.Relacionar_1_1(
        this,
        nameof(Cliente),
        Datos.Clientes);
```

El atributo indica:

```text
CampoOrigen  = clienteId
CampoDestino = id
```

El ORM busca:

```text
Venta.clienteId == Cliente.id
```

| Resultado                          | Qué ocurre                           |
| ---------------------------------- | ------------------------------------ |
| Una coincidencia                   | Devuelve ese objeto.                 |
| Ninguna coincidencia               | Devuelve `null`.                     |
| Más de una coincidencia            | Lanza `InvalidOperationException`.   |
| `clienteId` es `null` (`int?`)     | Devuelve `null` sin recorrer la tabla. |

---

## 4.3 Relación 1 a N directa

Se utiliza:

```csharp
ORM.Relacionar_1_N<T>(objeto, nombrePropiedad, tabla)
```

Ejemplo:

```csharp
[Relacion_1_N("id", "clienteId")]
public IReadOnlyList<Venta> Ventas =>
    ORM.Relacionar_1_N(
        this,
        nameof(Ventas),
        Datos.Ventas);
```

El ORM busca todas las ventas cuyo:

```text
Venta.clienteId == Cliente.id
```

Por ejemplo:

```text
Cliente id = 1

Ventas:
┌────┬─────────┐
│ id │clienteId│
├────┼─────────┤
│ 10 │    1    │ ←
│ 11 │    2    │
│ 12 │    1    │ ←
│ 13 │    3    │
└────┴─────────┘
```

Resultado:

```text
[Venta 10, Venta 12]
```

Los resultados salen **en el orden de la tabla**.

El resultado se expone como:

```csharp
IReadOnlyList<Venta>
```

para evitar que quien utiliza la propiedad pueda modificar directamente la lista de relaciones.

Si no hay coincidencias, o si la clave del objeto es `null`, el resultado es una lista vacía (nunca `null`).

---

## 4.4 Relación 1 a 1 mediante tabla intermedia

Se utiliza:

```csharp
ORM.Relacionar_1_1_Tabla<T>(objeto, nombrePropiedad, tablaRelacion, tablaDestino)
```

Supongamos:

```text
Persona
   │
   ▼
PersonaPasaporte
   │
   ▼
Pasaporte
```

La tabla intermedia podría contener:

```text
PersonaPasaporte
┌─────────┬────────────┐
│personaId│pasaporteId │
├─────────┼────────────┤
│    1    │     50     │
│    2    │     51     │
└─────────┴────────────┘
```

La propiedad podría ser:

```csharp
[Relacion_1_1_Tabla(
    typeof(PersonaPasaporte),
    "id",
    "personaId",
    "pasaporteId",
    "id")]
public Pasaporte? Pasaporte =>
    ORM.Relacionar_1_1_Tabla(
        this,
        nameof(Pasaporte),
        Datos.PersonaPasaportes,
        Datos.Pasaportes);
```

Los cinco argumentos del atributo, en orden, son:

| # | Nombre                 | Ejemplo                | Significado                                                |
| - | ---------------------- | ---------------------- | ---------------------------------------------------------- |
| 1 | `TipoTabla`            | `typeof(PersonaPasaporte)` | Tipo de los objetos de la tabla intermedia.            |
| 2 | `CampoOrigen`          | `"id"`                 | Campo del objeto actual (`Persona`).                       |
| 3 | `CampoTablaOrigen`     | `"personaId"`          | Campo de la tabla intermedia que apunta al objeto actual.  |
| 4 | `CampoTablaDestino`    | `"pasaporteId"`        | Campo de la tabla intermedia que apunta al destino.        |
| 5 | `CampoDestino`         | `"id"`                 | Campo del objeto destino (`Pasaporte`).                    |

El ORM realiza conceptualmente:

```text
Persona.id
   ↓
PersonaPasaporte.personaId
   ↓
PersonaPasaporte.pasaporteId
   ↓
Pasaporte.id
```

| Resultado                                          | Qué ocurre                         |
| -------------------------------------------------- | ---------------------------------- |
| Los datos llevan a un único `Pasaporte`            | Lo devuelve.                       |
| No hay filas intermedias o no hay destino          | Devuelve `null`.                   |
| Los datos llevan a más de un `Pasaporte`           | Lanza `InvalidOperationException`. |
| `Persona.id` es `null` (`int?`)                    | Devuelve `null`.                   |

Varias filas intermedias que apunten al **mismo** destino (`1→50`, `1→50`) cuentan como un solo resultado.

---

## 4.5 Relación 1 a N mediante tabla intermedia

Se utiliza:

```csharp
ORM.Relacionar_1_N_Tabla<T>(objeto, nombrePropiedad, tablaRelacion, tablaDestino)
```

Ejemplo:

```text
Alumno
   │
   ├── AlumnoMateria ──► Matemática
   ├── AlumnoMateria ──► Historia
   └── AlumnoMateria ──► Programación
```

La propiedad podría ser:

```csharp
[Relacion_1_N_Tabla(
    typeof(AlumnoMateria),
    "id",
    "alumnoId",
    "materiaId",
    "id")]
public IReadOnlyList<Materia> Materias =>
    ORM.Relacionar_1_N_Tabla(
        this,
        nameof(Materias),
        Datos.AlumnoMaterias,
        Datos.Materias);
```

El ORM:

1. Obtiene el `id` del alumno.
2. Recorre `AlumnoMaterias` y junta los `materiaId` de las filas con ese `alumnoId`.
3. Recorre `Materias` **una sola vez** y se queda con las materias cuyo `id` está entre los `materiaId` juntados.
4. Devuelve las materias encontradas, en el orden de la tabla `Materias`.

---

## 4.6 Claves opcionales (`null`)

Una clave foránea puede declararse con `?`:

```csharp
[Atributos.Columna("clienteId")]
private int? clienteId;
```

```csharp
[Relacion_1_1("clienteId", "id")]
public Cliente? Cliente => ...;
```

| Situación                                      | Resultado                                          |
| ---------------------------------------------- | -------------------------------------------------- |
| `int?` con valor frente a `int`                | Se comparan como `int`; la relación funciona.      |
| `clienteId == null` en el objeto actual        | "Sin relación": `null` o lista vacía. No es error. |
| Campo del destino con valor `null`             | Ese objeto nunca coincide.                         |

Dos valores `null` **no** se consideran iguales entre sí: un `null` nunca relaciona objetos.

Cuando la tabla se carga con `CsvManager`, una clave nula se guarda en el archivo como la palabra `null`, sin comillas (ver la documentación de `CsvManager`).

---

## 4.7 Herencia

Los campos pueden estar declarados en una clase base.

```text
Persona
  └── id

Empleado : Persona
  └── legajoId
```

El ORM busca cada campo en la clase del objeto y, si no lo encuentra, en sus clases base. Lo mismo ocurre con el tipo destino y con el tipo de la tabla intermedia.

La **propiedad** de la relación se busca desde la clase más derivada hacia la base y se usa la primera que aparece, sea cual sea su visibilidad. Si una clase derivada oculta la propiedad con `new`, se usa la de la clase derivada. Una propiedad privada declarada en una clase base también se encuentra cuando el objeto es de una clase derivada.

El tipo del objeto que se inspecciona es su tipo **real** en ejecución (`objeto.GetType()`), aunque se lo manipule mediante una variable del tipo base.

---

## 4.8 Guardar el resultado en la propiedad (`??=`)

Para las relaciones 1 a 1 se puede evitar repetir la búsqueda:

```csharp
private Cliente? cliente;

[Relacion_1_1("clienteId", "id")]
public Cliente? Cliente =>
    cliente ??= ORM.Relacionar_1_1(this, nameof(Cliente), Datos.Clientes);
```

Consideraciones:

* Si el resultado es `null` (no hay cliente), `??=` vuelve a buscar en el siguiente acceso, porque `null` significa "todavía no calculado".
* Si cambia el valor de `clienteId` o el contenido de la tabla, el objeto guardado puede quedar desactualizado. Para forzar una nueva búsqueda se asigna `cliente = null`.
* En relaciones 1 a N el ORM devuelve una lista nueva en cada llamada. Se puede guardar con el mismo patrón en un campo `IReadOnlyList<T>?`, con las mismas consideraciones.

---

## 4.9 Ejemplo completo

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

    private Cliente? cliente;

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

Uso:

```csharp
Datos.Clientes = CsvManager.ReadOrEmpty<Cliente>("datos/clientes.csv");
Datos.Ventas   = CsvManager.ReadOrEmpty<Venta>("datos/ventas.csv");

foreach (Cliente c in Datos.Clientes)
    Console.WriteLine($"{c.Nombre}: {c.Ventas.Count} ventas");
```

---

## 4.10 Buenas prácticas

* **Usar `nameof`** para el nombre de la propiedad.
* **Cargar todas las tablas** antes de acceder a cualquier relación.
* **Mantener ids únicos** en las tablas destino de relaciones 1 a 1.
* **Capturar `InvalidOperationException`** al probar una clase nueva: sus mensajes indican qué atributo, campo o dato está mal.
* **No modificar una tabla mientras se resuelve una relación** (ver sección 9).
* **No usar relaciones dentro de bucles sobre tablas enormes** sin considerar su costo (ver 7.12).

---

# 5. Tipos de relaciones

## 5.1 Resumen

| Método                 | Relación | Tabla intermedia | Devuelve           |
| ---------------------- | -------- | ---------------- | ------------------ |
| `Relacionar_1_1`       | 1 → 1    | No               | `T?`               |
| `Relacionar_1_N`       | 1 → N    | No               | `IReadOnlyList<T>` |
| `Relacionar_1_1_Tabla` | 1 → 1    | Sí               | `T?`               |
| `Relacionar_1_N_Tabla` | 1 → N    | Sí               | `IReadOnlyList<T>` |

---

## 5.2 Comparación visual

### 1 → 1 directa

```text
A ─────────► B
```

Se compara directamente un campo de `A` con uno de `B`.

---

### 1 → N directa

```text
       ┌──► B
A ─────┼──► B
       └──► B
```

Se buscan todos los objetos de `B` que coincidan.

---

### 1 → 1 con tabla

```text
A ───► Relación ───► B
```

La tabla intermedia conecta ambos objetos.

---

### 1 → N con tabla

```text
       ┌──► Relación ───► B
A ─────┼──► Relación ───► B
       └──► Relación ───► B
```

Puede haber varios registros en la tabla intermedia.

---

## 5.3 Qué devuelve cada método según los datos

| Situación                                 | `_1_1`     | `_1_N`        | `_1_1_Tabla` | `_1_N_Tabla`  |
| ----------------------------------------- | ---------- | ------------- | ------------ | ------------- |
| Clave del objeto actual `null`            | `null`     | lista vacía   | `null`       | lista vacía   |
| Ninguna coincidencia                      | `null`     | lista vacía   | `null`       | lista vacía   |
| Una coincidencia                          | el objeto  | lista de 1    | el objeto    | lista de 1    |
| Varias coincidencias                      | **error**  | lista de N    | **error**    | lista de N    |
| Elemento `null` dentro de una tabla       | se omite   | se omite      | se omite     | se omite      |

---

# 6. Manejo de errores

## 6.1 Principio general

El ORM intenta detectar errores de configuración antes de producir errores difíciles de entender.

Por ejemplo, en lugar de permitir:

```csharp
propiedad.GetCustomAttribute<...>()!.GetType();
```

y obtener un:

```text
NullReferenceException
```

se genera un mensaje indicando qué propiedad está mal configurada.

## 6.2 Excepciones que pueden lanzarse

| Excepción                   | Cuándo                                                                                           |
| --------------------------- | ------------------------------------------------------------------------------------------------ |
| `ArgumentNullException`     | `objeto`, `tabla`, `tablaRelacion` o `tablaDestino` es `null`.                                   |
| `ArgumentException`         | `nombrePropiedad` es `null`, vacío o solo espacios.                                              |
| `InvalidOperationException` | Error de configuración (propiedad, atributo, campo, tipos) o de datos (varias coincidencias en 1 → 1, objeto de tipo incorrecto en la tabla intermedia). |

## 6.3 Catálogo de mensajes

Todos los siguientes son `InvalidOperationException`, salvo el primero.

### Argumentos

| Mensaje                                                                  | Causa                                  | Solución                                  |
| ------------------------------------------------------------------------ | -------------------------------------- | ----------------------------------------- |
| `El nombre de la propiedad no puede estar vacío. Use nameof(Propiedad).` | `nombrePropiedad` vacío (`ArgumentException`). | Pasar `nameof(Propiedad)`.        |

### Configuración

| Mensaje                                                                                          | Causa                                                                  | Solución                                                  |
| ------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------- | --------------------------------------------------------- |
| `No existe la propiedad de instancia X en el tipo Y.`                                            | El nombre no coincide, o la propiedad es `static`.                     | Usar `nameof` y declarar la propiedad como de instancia.  |
| `La propiedad X de Y no tiene el atributo Z.`                                                    | La propiedad no tiene el atributo del método llamado.                  | Agregar el atributo correcto sobre la propiedad.          |
| `Un atributo de relación indica un nombre de campo vacío para el tipo X.`                        | Un argumento del atributo es `null`, vacío o solo espacios.            | Completar el nombre del campo.                            |
| `No existe el campo 'n' en el tipo X ni en ninguno de sus tipos base. ... Campos disponibles: ...` | El nombre del atributo no es un campo (por ejemplo, es una propiedad) o difiere en mayúsculas. | Elegir uno de los campos listados en el mensaje. |
| `Los campos no tienen tipos compatibles: A.x (Int32) y B.y (String). Deben ser del mismo tipo ...` | Los dos campos comparados tienen tipos distintos.                    | Igualar los tipos (se permite `int` con `int?`).          |
| `El atributo de relación de X no indica el tipo de la tabla intermedia.`                         | El atributo de tabla intermedia tiene `TipoTabla` en `null`.           | Indicar `typeof(...)`.                                    |
| `La tabla intermedia contiene un objeto de tipo X, pero se esperaba Y.`                          | La lista intermedia contiene objetos que no son del tipo del atributo. | Pasar la tabla correcta.                                  |

### Datos

| Mensaje                                                                      | Causa                                                                         | Solución                                                         |
| ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| `La relación 1→1 de X encontró más de un objeto relacionado en Y.`           | Más de un objeto destino cumple la relación (típicamente, ids duplicados).    | Corregir los datos, o usar una relación 1 → N si es lo esperado. |

## 6.4 Relación sin coincidencias

Que no exista un objeto relacionado no es un error.

En una relación 1 → 1:

```csharp
Cliente? cliente
```

puede resultar:

```csharp
null
```

En una relación 1 → N:

```csharp
IReadOnlyList<Venta>
```

puede resultar:

```text
[]
```

Esto permite representar relaciones opcionales o simplemente objetos que todavía no tienen relaciones.

## 6.5 Patrón de captura recomendado

```csharp
try
{
    Cliente? cliente = venta.Cliente;
}
catch (InvalidOperationException ex)
{
    // Configuración incorrecta o datos inconsistentes: ex.Message indica cuál.
}
```

> `ArgumentNullException` hereda de `ArgumentException`. Un `catch (ArgumentException)` atrapa ambas.

## 6.6 Cómo diagnosticar

```text
¿Qué excepción apareció?
│
├─ ArgumentNullException
│     → Una tabla (o el objeto) es null. Revisar que Datos.X esté cargado.
│
├─ ArgumentException
│     → El nombre de la propiedad está vacío.
│
└─ InvalidOperationException → leer el mensaje:
      ├─ "No existe la propiedad..."         → nombre mal escrito o propiedad estática.
      ├─ "no tiene el atributo..."           → falta el atributo, o es de otro tipo de relación.
      ├─ "No existe el campo..."             → el atributo nombra una propiedad o usa otras mayúsculas.
      ├─ "tipos compatibles..."              → los campos comparados son de tipos distintos.
      ├─ "tabla intermedia contiene..."      → se pasó una tabla que no es la del atributo.
      └─ "encontró más de un objeto..."      → ids duplicados en la tabla destino.
```

---

# 7. Funcionamiento interno

## 7.1 Mapa de métodos

```text
ORM
│
├── Públicos
│   ├── Relacionar_1_1<T>()
│   ├── Relacionar_1_N<T>()
│   ├── Relacionar_1_1_Tabla<T>() ──┐
│   └── Relacionar_1_N_Tabla<T>() ──┴─▶ ResolverConTabla<T>()  (núcleo común)
│
├── Reflection
│   ├── ObtenerPropiedad()
│   ├── ObtenerAtributo<TAtributo>()
│   ├── ObtenerCampo()  ──▶ BuscarCampo(), ListarCampos()
│
├── Validación y comparación
│   ├── ValidarTipos()  ──▶ Describir()
│   ├── ValidarTipoTabla()
│   └── ValoresIguales()
│
└── Cachés
    ├── cachePropiedades  (Type, nombre)           → PropertyInfo
    ├── cacheCampos       (Type, nombre)           → FieldInfo
    └── cacheAtributos    (PropertyInfo, Type)     → Attribute
```

---

## 7.2 `Relacionar_1_1`

Conceptualmente realiza:

```text
Validar objeto y tabla (no null)
     │
     ▼
Obtener propiedad  ──▶  Leer atributo Relacion_1_1
     │
     ▼
Obtener campo origen (tipo del objeto) y campo destino (tipo T)
     │
     ▼
Validar que los tipos sean compatibles
     │
     ▼
Leer valor del origen ── ¿null? ──▶ devolver null
     │
     ▼
Recorrer TODA la tabla destino:
     · elemento null → omitir
     · valores distintos → omitir
     · coincide y ya había una → ERROR (más de un objeto)
     · coincide → recordarlo
     │
     ▼
Devolver el objeto recordado (o null)
```

Se recorre toda la tabla aunque ya se haya encontrado una coincidencia: así se detecta una segunda, que sería una inconsistencia de los datos (por ejemplo, ids duplicados).

Por ejemplo:

```text
Venta.clienteId = 5

Clientes:
id = 1
id = 3
id = 5  ← encontrado
id = 8
```

Resultado:

```text
Cliente con id = 5
```

La tabla **no se copia**: se enumera directamente.

---

## 7.3 `Relacionar_1_N`

La diferencia principal es que no lanza error por varias coincidencias: las agrega todas.

Conceptualmente:

```text
Para cada objeto destino (no null):

    si campoDestino == valorOrigen:
        agregar a resultado
```

Por ejemplo:

```text
Cliente.id = 3

Venta.clienteId:
1
3 ←
2
3 ←
3 ←
5
```

Resultado:

```text
Venta 2
Venta 4
Venta 5
```

El resultado se devuelve envuelto con `AsReadOnly()`. Si la clave del objeto es `null`, se devuelve `Array.Empty<T>()`.

---

## 7.4 `ResolverConTabla`: núcleo de las relaciones con tabla intermedia

Las dos relaciones con tabla intermedia comparten este método. La diferencia entre ellas está solo en lo que hacen con el resultado:

```text
Relacionar_1_N_Tabla  → devuelve la lista (solo lectura)
Relacionar_1_1_Tabla  → 0 elementos → null
                        1 elemento  → ese elemento
                        2 o más     → InvalidOperationException
```

### Algoritmo (dos pasadas)

```text
Preparación:
   · tipoTabla no puede ser null.
   · Obtener los cuatro campos:
        campoOrigen        (en el objeto actual)
        campoTablaOrigen   (en la tabla intermedia)
        campoTablaDestino  (en la tabla intermedia)
        campoDestino       (en el tipo destino)
   · Validar tipos:  campoOrigen ↔ campoTablaOrigen
                     campoTablaDestino ↔ campoDestino
   · Leer valorOrigen. Si es null → resultado vacío.

Pasada 1 — tabla intermedia:
   Para cada fila (no null):
        · comprobar que sea del tipo esperado
        · si campoTablaOrigen == valorOrigen:
              agregar campoTablaDestino (si no es null) a un HashSet de claves

   Si el HashSet quedó vacío → resultado vacío (no se recorre la tabla destino).

Pasada 2 — tabla destino (una sola vez):
   Para cada objeto (no null):
        · si su campoDestino está en el HashSet → agregarlo al resultado
```

### Ejemplo

```text
Alumno.id = 1

AlumnoMateria (alumnoId, materiaId):
  (1, 5)  ← clave 5
  (1, 5)  ← clave 5 (repetida: el HashSet la une)
  (1, 8)  ← clave 8
  (2, 5)  ← no pertenece al alumno 1

claves = { 5, 8 }

Materias:
  id = 5  ← sí
  id = 8  ← sí
  id = 9  ← no

Resultado: [Materia 5, Materia 8]
```

### Propiedades del resultado

| Propiedad                                  | Detalle                                                                                                  |
| ------------------------------------------ | -------------------------------------------------------------------------------------------------------- |
| **Sin duplicados por filas repetidas**     | Las filas repetidas de la tabla intermedia producen una sola clave.                                      |
| **Orden determinista**                     | Los objetos salen en el orden de la tabla destino, no en el de la tabla intermedia.                      |
| **No depende de `Equals` de `T`**          | El conjunto contiene **claves**, no objetos. Dos objetos distintos con los mismos valores no se fusionan. |
| **Destinos con la misma clave**            | Si dos objetos de la tabla destino tienen el mismo `id`, **ambos** se incluyen (y en 1 → 1 provoca error). |
| **Sin trabajo innecesario**                | Si el objeto no tiene filas intermedias, la tabla destino no se recorre.                                 |

---

## 7.5 Reflection

El ORM necesita trabajar con campos cuyos nombres se reciben como texto.

Por ejemplo:

```csharp
string nombre = "clienteId";
```

Reflection permite convertir ese nombre en información real de C#:

```csharp
FieldInfo campo =
    ObtenerCampo(typeof(Venta), nombre);
```

Luego:

```csharp
object? valor =
    campo.GetValue(venta);
```

puede obtener el valor aunque el campo sea privado.

Esto permite que el ORM funcione con diferentes clases sin escribir un método específico para cada una.

---

## 7.6 `ObtenerPropiedad`

Busca la propiedad que representa la relación.

```text
nombre vacío o solo espacios ──▶ ArgumentException
      │
      ▼
¿Está en el caché? ── sí ──▶ devolverla
      │ no
      ▼
Recorrer la jerarquía, de la clase más derivada hacia la base:
   en cada clase: GetProperty(nombre, Instance | Public | NonPublic | DeclaredOnly)
      ├── encontrada → guardar en caché y devolverla
      └── no → pasar a la clase base
      │
      ▼
Nunca encontrada ──▶ InvalidOperationException
```

**Por qué `DeclaredOnly`:** si una clase derivada oculta la propiedad con `new`, una búsqueda única en toda la jerarquía encontraría dos propiedades con el mismo nombre y .NET lanzaría `AmbiguousMatchException`. Mirando un nivel a la vez se usa siempre la más derivada.

**Por qué `NonPublic`:** permite que la propiedad de la relación sea privada, protegida o interna. Además, una búsqueda sobre la clase derivada no devuelve las propiedades *privadas* declaradas en una clase base; al consultar cada nivel por separado, esas propiedades también se encuentran.

---

## 7.7 `ObtenerAtributo`

Lee el atributo de la propiedad y lo guarda en caché, indexado por `(propiedad, tipo de atributo)`.

Si la propiedad no tiene el atributo pedido, lanza `InvalidOperationException` indicando propiedad, clase y atributo, en lugar de dejar que ocurra un `NullReferenceException`.

---

## 7.8 `ObtenerCampo` y `BuscarCampo`

`ObtenerCampo` busca un campo y genera un error claro si no existe.

```text
nombre null o vacío ──▶ InvalidOperationException
      │
      ▼
¿Está en el caché? ── sí ──▶ devolverlo
      │ no
      ▼
BuscarCampo(tipo, nombre)
      ├── encontrado → guardar en caché y devolverlo
      └── null → InvalidOperationException con la lista de campos disponibles
```

`BuscarCampo` recorre la jerarquía de herencia:

```text
Empleado
   │
   ▼
Persona
   │
   ▼
object
```

En cada clase usa `GetField` con `Instance | Public | NonPublic | DeclaredOnly`. Si el campo no está en `Empleado`, continúa con `Persona`.

**Por qué se recorre la jerarquía:** `GetField` con `NonPublic` no devuelve los campos *privados* heredados. Con `DeclaredOnly` cada clase aporta los suyos.

### `ListarCampos`

Para el mensaje de "campo inexistente", enumera los campos de instancia del tipo y de sus bases (sin incluir `object`). Omite los campos generados por el compilador (los que empiezan con `<`, como `<Id>k__BackingField`).

El error más frecuente es escribir en el atributo el nombre de una **propiedad** (`"Id"`) en lugar del campo (`"id"`). El listado permite corregirlo de inmediato.

---

## 7.9 `ValidarTipos`

Compara los tipos de dos campos **sin considerar `Nullable<>`**:

```text
int?  → int
int   → int
```

Si los tipos resultantes difieren, lanza `InvalidOperationException` con los dos campos descritos (`Clase.campo (Tipo)`, con `?` si es nullable).

Cuando se compara un `int?` con valor `5`, .NET lo convierte en un `int` 5 "boxeado"; por eso `Equals(5, 5)` da `true` y la relación funciona.

---

## 7.10 `ValidarTipoTabla`

Comprueba que cada fila de la tabla intermedia sea una instancia del tipo indicado en el atributo (`IsInstanceOfType`). Las clases derivadas también son válidas.

---

## 7.11 `ValoresIguales`

```text
alguno es null → true solo si AMBOS son null
si no          → Equals(valor1, valor2)
```

Como los métodos públicos descartan de antemano una clave de origen `null`, en la práctica un `null` nunca relaciona dos objetos.

`Equals` se aplica sobre valores "boxeados": números, `string`, `Guid`, `enum`, etc. Las cadenas se comparan distinguiendo mayúsculas.

Esta comparación centralizada mantiene el comportamiento de las cuatro relaciones en un único lugar.

---

## 7.12 Complejidad

Sea `m` la cantidad de objetos de la tabla destino y `n` la de la tabla intermedia.

| Método                 | Costo por acceso |
| ---------------------- | ---------------- |
| `Relacionar_1_1`       | O(m)             |
| `Relacionar_1_N`       | O(m)             |
| `Relacionar_1_1_Tabla` | O(n + m)         |
| `Relacionar_1_N_Tabla` | O(n + m)         |

Cada acceso **recorre las tablas completas**: no hay índices. Resolver la relación de **todos** los objetos de una tabla de `N` elementos cuesta, por lo tanto, aproximadamente O(N × m).

Los cachés eliminan la repetición de las búsquedas de Reflection, pero no el recorrido de las tablas ni la lectura de cada campo con `GetValue`.

---

## 7.13 Cachés y concurrencia

Los tres cachés son `ConcurrentDictionary`, seguros entre hilos. Se guardan solo los éxitos:

```text
Éxito  → se guarda; las siguientes llamadas lo reutilizan.
Error  → no se guarda; se vuelve a lanzar en cada llamada.
```

Las **tablas** no están sincronizadas: el ORM no bloquea nada sobre ellas.

---

## 7.14 `IReadOnlyList<T>`

Las relaciones 1 → N devuelven:

```csharp
IReadOnlyList<T>
```

Esto permite:

```csharp
ventas.Count
ventas[0]

foreach (Venta venta in ventas)
{
    ...
}
```

pero no:

```csharp
ventas.Add(...)
ventas.Remove(...)
ventas.Clear()
```

La idea es que la colección devuelta represente el resultado de una relación, no una lista que el código externo pueda modificar libremente.

---

# 8. Decisiones de diseño

Estas elecciones son razonables pero discutibles. Se documentan para poder cambiarlas con conocimiento.

| Decisión                                         | Alternativa                              | Motivo de la elección                                                                         |
| ------------------------------------------------ | ---------------------------------------- | --------------------------------------------------------------------------------------------- |
| Relaciones lazy                                  | Cargar todas al crear los objetos        | Evita buscar relaciones que nunca se utilizan.                                                |
| Atributos sobre propiedades                      | Configuración externa                    | La relación queda declarada junto al código que la utiliza.                                   |
| Cuatro métodos de relación                       | Un único método con un enum              | Cada tipo de relación tiene una operación claramente diferenciada.                            |
| Campos como claves                               | Propiedades                              | Coincide con el sistema de persistencia de `CsvManager`.                                      |
| `where T : class`                                | Permitir `struct`                        | Una relación devuelve objetos o `null`; con `struct` `T?` tendría otro significado.           |
| `IReadOnlyList<T>` para 1 → N                    | `List<T>` pública                        | Evita modificaciones accidentales.                                                            |
| Recorrer toda la tabla en 1 → 1                  | Detenerse en la primera coincidencia     | Permite detectar ids duplicados en lugar de elegir uno en silencio.                           |
| Conjunto de **claves** en tablas intermedias     | Conjunto de objetos o diccionario        | Une filas repetidas, no depende de `Equals` de `T`, conserva el orden de la tabla destino y evita recorrer la tabla destino si no hay filas intermedias. |
| Núcleo común `ResolverConTabla`                  | Una copia de la lógica por método        | Un solo lugar para corregir; las dos relaciones se comportan igual.                           |
| Destinos con la misma clave se incluyen todos    | Lanzar error en 1 → N con tabla          | En 1 → 1 el error aparece igualmente; en 1 → N no se descartan datos.                         |
| Clave `null` = "sin relación"                    | Error                                    | Permite claves foráneas opcionales.                                                           |
| `int?` compatible con `int`                      | Exigir tipos idénticos                   | Una clave foránea opcional no debería requerir una conversión manual.                         |
| Validaciones explícitas                          | Permitir `NullReferenceException`        | Produce errores más claros.                                                                   |
| Elementos `null` de una tabla se omiten          | Lanzar error                             | Un elemento `null` no puede relacionarse con nada; ignorarlo evita fallos de Reflection.      |
| Cachés de Reflection (solo éxitos)               | Buscar siempre                           | Las relaciones se resuelven en cada acceso; los errores de configuración siguen siendo visibles. |
| No copiar la tabla al recorrerla                 | Copiar con `ToList()`                    | Evita una copia completa en cada acceso.                                                      |
| Tablas recibidas como parámetros                 | Acceso interno a archivos                | El ORM trabaja con objetos ya cargados y no depende de `CsvManager`.                          |
| Cache del objeto relacionado en la propiedad     | Cache dentro del ORM                     | El ORM queda sin estado propio sobre los objetos; el programador decide cuándo invalidar.     |

---

# 9. Limitaciones conocidas

| Limitación                      | Detalle                                                                                                                                    |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| **Datos en memoria**            | El ORM no carga archivos por sí mismo.                                                                                                     |
| **Tablas cargadas**             | Las tablas deben existir antes de resolver relaciones.                                                                                     |
| **Claves válidas**              | Los identificadores deben tener valores coherentes.                                                                                        |
| **Integridad referencial**      | Un ID que no existe no puede producir un objeto relacionado.                                                                               |
| **Cache en la propiedad**       | Un objeto guardado con `??=` puede quedar desactualizado si cambia el identificador o la tabla.                                            |
| **Modificaciones concurrentes** | No se debe modificar una tabla mientras el ORM la recorre. Como no se copia, una modificación simultánea provoca una excepción de colección modificada (distinta de las del ORM) o resultados incoherentes. |
| **Tablas diferidas**            | Si una tabla es una consulta diferida (por ejemplo, un `Where` de LINQ), se evalúa en cada acceso.                                         |
| **Sin índices**                 | Cada acceso recorre las tablas completas (ver 7.12). Con tablas enormes y muchos accesos puede ser lento.                                  |
| **Reflection**                  | Es más flexible que el acceso directo, pero más lento.                                                                                     |
| **Nombres como texto**          | Los nombres de campos de los atributos se comprueban durante la ejecución, no al compilar.                                                 |
| **Solo campos**                 | Las claves deben ser campos; una propiedad (incluidas las automáticas) no sirve como clave.                                                |
| **Solo propiedades de instancia** | La propiedad que expone la relación no puede ser `static`. Su visibilidad es indiferente.                                                |
| **Atributos sin validación propia** | Los atributos solo almacenan texto; los errores de nombres o tipos aparecen al resolver la relación, no al compilar.                  |
| **Tablas intermedias de clase** | La tabla intermedia debe ser de un tipo clase para poder pasarse como `IEnumerable<object>`.                                               |
| **Igualdad por `Equals`**       | Las claves se comparan con `Equals` sobre valores boxeados; los tipos de clave deben tener una igualdad adecuada (números, `string`, `Guid`, `enum`). |
| **Relaciones complejas**        | El sistema soporta las cuatro formas definidas, pero no es un ORM completo como Entity Framework.                                          |
| **Cascadas**                    | El ORM no elimina ni crea automáticamente objetos relacionados.                                                                            |
| **Transacciones**               | No administra transacciones entre varias tablas.                                                                                           |
| **Integridad automática**       | No corrige IDs duplicados o inexistentes automáticamente; solo detecta duplicados en las relaciones 1 → 1.                                 |
| **Hilos**                       | Los cachés son seguros entre hilos; las tablas no.                                                                                         |
| **Misma instancia repetida**    | Si la misma instancia aparece dos veces en una tabla destino, cuenta como dos coincidencias: en 1 → 1 produce error y en 1 → N aparece dos veces en el resultado. |

---

# 10. Posibles dudas

## 10.1 ¿El ORM guarda objetos en el CSV?

No.

El `CsvManager` guarda los datos simples:

```text
id,clienteId
10,3
```

El ORM utiliza esos datos para reconstruir las relaciones en memoria.

---

## 10.2 ¿Por qué no guardar directamente el `Cliente`?

Porque un objeto relacionado puede contener muchos datos y, además, puede estar relacionado con otros objetos.

Guardar:

```csharp
private Cliente cliente;
```

directamente en el CSV obligaría a serializar todo el objeto.

En cambio:

```csharp
private int clienteId;
```

guarda únicamente la referencia.

---

## 10.3 ¿Por qué el campo relacionado no lleva `[Columna]`?

Porque el objeto relacionado:

```csharp
private Cliente? cliente;
```

no forma parte de los datos persistidos.

El dato persistido es:

```csharp
private int clienteId;
```

El objeto `Cliente` se obtiene mediante el ORM.

---

## 10.4 ¿Por qué la relación se hace en una propiedad?

Permite utilizarla como si fuera un dato normal:

```csharp
venta.Cliente
```

sin tener que escribir manualmente:

```csharp
ORM.Relacionar_1_1(...);
```

cada vez que se necesita.

---

## 10.5 ¿Qué significa `??=`?

En:

```csharp
cliente ??= ORM.Relacionar_1_1(...);
```

significa:

```text
Si cliente es null:
    buscar el cliente y guardarlo.

Si cliente ya tiene un valor:
    utilizar ese valor.
```

Esto permite implementar el comportamiento lazy con cache.

---

## 10.6 ¿Qué pasa si no existe el objeto relacionado?

En una relación 1 → 1:

```csharp
Cliente?
```

puede devolver:

```csharp
null
```

En una relación 1 → N:

```csharp
IReadOnlyList<Venta>
```

puede devolver una colección vacía.

Esto no representa un error.

---

## 10.7 ¿Por qué usar `nameof`?

En lugar de:

```csharp
ORM.Relacionar_1_1(
    this,
    "Cliente",
    Datos.Clientes);
```

se puede escribir:

```csharp
ORM.Relacionar_1_1(
    this,
    nameof(Cliente),
    Datos.Clientes);
```

`nameof` obtiene el nombre directamente del elemento de C#.

Si posteriormente se cambia el nombre de la propiedad, el compilador puede detectar el cambio.

---

## 10.8 ¿Por qué el ORM usa Reflection?

Porque necesita funcionar con diferentes clases.

Sin Reflection habría que escribir algo específico para cada relación:

```csharp
venta.clienteId == cliente.id
```

Con Reflection, el ORM puede recibir:

```text
"clienteId"
"id"
```

y descubrir los campos durante la ejecución.

Esto permite reutilizar el mismo código para muchas clases.

---

## 10.9 ¿Por qué hay cuatro métodos?

Porque existen cuatro casos diferentes:

```text
1 → 1
1 → N
1 → 1 con tabla intermedia
1 → N con tabla intermedia
```

Cada método representa directamente uno de esos casos.

Esto evita tener que pasar un enum indicando la cardinalidad y luego interpretar ese enum dentro de un método gigante.

---

## 10.10 ¿Por qué hay cuatro atributos?

Por la misma razón.

Cada atributo describe exactamente una forma de relación:

```csharp
Relacion_1_1
Relacion_1_N
Relacion_1_1_Tabla
Relacion_1_N_Tabla
```

Los atributos contienen los nombres de los campos necesarios para realizar la búsqueda. Cada método exige **su** atributo: llamar a `Relacionar_1_N` sobre una propiedad marcada con `Relacion_1_1` produce un error de atributo faltante.

---

## 10.11 ¿Puedo relacionar clases heredadas?

Sí.

`BuscarCampo` recorre las clases base, por lo que un campo declarado en una clase base puede utilizarse en una relación de una clase derivada.

Por ejemplo:

```text
Persona
  └── id

Empleado : Persona
```

El ORM puede encontrar `id` aunque esté declarado en `Persona`.

---

## 10.12 ¿Qué pasa si cambio un ID después de resolver una relación?

Si se utiliza cache en la propiedad:

```csharp
cliente ??= ...
```

el objeto relacionado ya obtenido puede permanecer almacenado aunque cambie el identificador.

Por eso, si se permite modificar las claves, hay que invalidar el cache correspondiente.

Por ejemplo:

```csharp
cliente = null;
```

antes de volver a resolver la relación.

Los cachés internos del ORM no tienen este problema: guardan solo información de Reflection (propiedades, atributos, campos), nunca valores ni objetos de las tablas.

---

## 10.13 ¿Por qué no se busca la relación cada vez?

Porque sería innecesariamente costoso.

Sin cache:

```text
venta.Cliente
   ↓ buscar

venta.Cliente
   ↓ buscar otra vez

venta.Cliente
   ↓ buscar otra vez
```

Con cache:

```text
venta.Cliente
   ↓ buscar
   ↓ guardar

venta.Cliente
   ↓ devolver cache

venta.Cliente
   ↓ devolver cache
```

---

## 10.14 ¿Por qué no se cargan todas las relaciones al principio?

Porque podría haber muchas relaciones que nunca se utilizan.

Por ejemplo, si se cargan:

```text
10.000 ventas
50.000 productos
100.000 clientes
```

y solamente se necesita mostrar una venta, cargar todas sus relaciones desde el principio sería innecesario.

El enfoque lazy permite cargar una relación solamente cuando se necesita.

---

## 10.15 ¿El ORM reemplaza a `CsvManager`?

No.

Cada componente tiene una responsabilidad diferente:

```text
CsvManager
    ↓
Persistencia
    ↓
CSV ↔ objetos

ORM
    ↓
Relaciones
    ↓
objeto ↔ objeto
```

Se complementan.

---

## 10.16 ¿El ORM reemplaza a una base de datos SQL?

No.

Este ORM es una implementación pequeña orientada al proyecto.

No proporciona características propias de sistemas de bases de datos completos, como:

* consultas SQL;
* transacciones;
* índices persistentes;
* restricciones de clave foránea administradas por el motor;
* concurrencia entre procesos;
* optimización avanzada de consultas.

Su objetivo es permitir trabajar cómodamente con objetos almacenados en archivos CSV.

---

## 10.17 ¿Qué pasa si hay un ID que no existe?

Por ejemplo:

```text
Venta.clienteId = 99
```

pero:

```text
Clientes:
1
2
3
```

No hay ningún cliente con ID 99.

La relación 1 → 1 no puede devolver un objeto válido y devuelve `null`.

Esto puede considerarse un problema de **integridad referencial** de los datos.

---

## 10.18 ¿Qué pasa si hay IDs duplicados?

Por ejemplo:

```text
Cliente
id = 1
id = 1
```

| Relación                          | Resultado                                                  |
| --------------------------------- | ---------------------------------------------------------- |
| `Relacionar_1_1`                  | `InvalidOperationException` (más de un objeto relacionado). |
| `Relacionar_1_1_Tabla`            | `InvalidOperationException`, si la clave es alcanzada desde el objeto actual. |
| `Relacionar_1_N`                  | Se incluyen ambos objetos.                                  |
| `Relacionar_1_N_Tabla`            | Se incluyen ambos objetos.                                  |

El ORM no elige arbitrariamente uno de ellos en las relaciones 1 → 1.

---

## 10.19 ¿Por qué las relaciones con tabla intermedia usan un conjunto de claves?

Porque buscar cada objeto destino recorriendo toda la tabla para cada fila intermedia sería muy lento.

Con el conjunto de claves:

```text
Pasada 1: tabla intermedia → conjunto de claves   (n pasos)
Pasada 2: tabla destino    → ¿su clave está en el conjunto?   (m pasos)
```

Se obtiene un costo aproximado de:

```text
O(n + m)
```

en lugar de:

```text
O(n × m)
```

Además, el conjunto contiene **claves** y no objetos, por lo que no depende de cómo la clase `T` implemente `Equals`.

---

## 10.20 ¿Por qué un conjunto y no solamente una lista?

Porque una tabla intermedia podría contener accidentalmente relaciones repetidas.

Por ejemplo:

```text
1 → 5
1 → 5
```

Con una lista de claves, la clave 5 aparecería dos veces. Con un conjunto (`HashSet`), la segunda aparición no se agrega, y como la tabla destino se recorre una sola vez, la materia 5 aparece una sola vez en el resultado.

---

## 10.21 ¿Puedo modificar la lista que devuelve una relación 1 → N?

No mediante la interfaz:

```csharp
IReadOnlyList<T>
```

Por ejemplo:

```csharp
cliente.Ventas.Add(venta);
```

no está permitido.

Esto protege la colección que representa la relación.

Para agregar una venta realmente hay que modificar la tabla correspondiente y luego guardar los datos con `CsvManager`.

---

## 10.22 ¿El ORM modifica los CSV?

No.

El ORM solamente trabaja con los objetos que ya están en memoria.

Para guardar cambios se utiliza:

```csharp
CsvManager.Write(...)
```

Por lo tanto:

```text
CSV
 │
 ▼
CsvManager.Read
 │
 ▼
Objetos en memoria
 │
 ▼
ORM
 │
 ▼
Relaciones entre objetos
```

Y para guardar:

```text
Objetos en memoria
 │
 ▼
CsvManager.Write
 │
 ▼
CSV
```

---

## 10.23 ¿Qué pasa si una tabla no fue cargada?

Si la tabla es `null`, se lanza `ArgumentNullException`.

Si es una lista vacía porque todavía no se cargaron los datos, la relación no encuentra objetos: devuelve `null` o una lista vacía, sin error. Por eso el programa debe cargar primero las tablas necesarias:

```csharp
Datos.Clientes =
    CsvManager.ReadOrEmpty<Cliente>("datos/clientes.csv");

Datos.Ventas =
    CsvManager.ReadOrEmpty<Venta>("datos/ventas.csv");
```

y después utilizar las propiedades relacionadas.

---

## 10.24 ¿Por qué me dice que no existe el campo si lo tengo declarado?

Las causas habituales:

| Causa                                              | Corrección                                       |
| -------------------------------------------------- | ------------------------------------------------ |
| El atributo nombra una **propiedad** (`"Id"`).     | Nombrar el **campo** (`"id"`).                   |
| Las mayúsculas no coinciden (`"ClienteId"`).       | Copiar el nombre exacto del campo.               |
| El campo está en otra clase (no en la del destino).| Revisar de qué tipo es la tabla destino.         |

El mensaje de error incluye la lista de campos disponibles del tipo.

---

## 10.25 ¿Puedo usar una clave `string` o `Guid`?

Sí. Cualquier tipo cuyo `Equals` compare por valor sirve: números, `string`, `Guid`, `enum`, `DateOnly`, etc. Las cadenas se comparan distinguiendo mayúsculas y minúsculas. Los dos campos deben ser del mismo tipo.

---

## 10.26 ¿Puedo pasar una tabla que no sea `List<T>`?

Sí. Cualquier `IEnumerable<T>` sirve: arreglos, `List<T>`, resultados de LINQ. Si es una consulta diferida, se vuelve a evaluar en cada acceso.

---

## 10.27 ¿Por qué el parámetro de la tabla intermedia es `IEnumerable<object>`?

Porque el ORM no conoce de antemano el tipo de esa tabla: lo toma del atributo (`TipoTabla`) y comprueba cada fila contra él. Gracias a la covariancia, una `List<AlumnoMateria>` se pasa sin conversión (siempre que `AlumnoMateria` sea una clase).

---

## 10.28 ¿Cuál es la idea general del sistema?

El sistema completo queda dividido en responsabilidades:

```text
                    ┌──────────────┐
                    │    CSV       │
                    └──────┬───────┘
                           │
                        Read/Write
                           │
                           ▼
                    ┌──────────────┐
                    │ CsvManager   │
                    └──────┬───────┘
                           │
                     objetos en memoria
                           │
                           ▼
                    ┌──────────────┐
                    │     ORM      │
                    └──────┬───────┘
                           │
                    relaciones
                           │
             ┌─────────────┴─────────────┐
             ▼                           ▼
         Objeto A                    Objeto B
```

De esta manera cada componente tiene una función concreta:

```text
CsvManager → guardar y recuperar datos.

ORM        → relacionar objetos.

Atributos  → describir cómo deben relacionarse.

Clases     → representar los datos del programa.
```

El resultado es un sistema pequeño de persistencia y relaciones que permite trabajar con objetos de C# sin tener que escribir manualmente el código de búsqueda para cada relación.
