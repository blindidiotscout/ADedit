# Makefile für das HIE MMC Plugin (MinGW / GCC)

CC=gcc
LD=gcc
RC=windres

# Ziel-DLL
TARGET=hie_mmc_plugin.dll

# Quelldateien
C_SRC=hie_mmc_plugin.c
RC_SRC=hie_mmc_plugin.rc
OBJ=hie_mmc_plugin.o
RES=hie_mmc_plugin.res

# Bibliotheken für COM und ADSI
LIBS=-lole32 -loleaut32 -lactiveds -ladsiid -luuid -lnetapi32

CFLAGS=-Wall -O2 -D_WIN32_WINNT=0x0600

all: $(TARGET)

$(TARGET): $(OBJ) $(RES)
	$(LD) -shared -o $@ $^ $(LIBS)

$(OBJ): $(C_SRC) hie_mmc_plugin.h
	$(CC) $(CFLAGS) -c $< -o $@

$(RES): $(RC_SRC) hie_mmc_plugin.h
	$(RC) $< -O coff -o $@

clean:
	del /Q $(OBJ) $(RES) $(TARGET) 2>nul || true
