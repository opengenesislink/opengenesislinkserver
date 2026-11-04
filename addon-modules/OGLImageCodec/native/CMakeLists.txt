cmake_minimum_required(VERSION 3.21)
project(OGLImageCodecNative LANGUAGES C)

# The OpenJPEG 2.x shim has its own ABI and is never linked against
# the old OpenMetaverse/openjpeg-dotnet native wrapper.
find_package(OpenJPEG 2.5 CONFIG REQUIRED)

if(NOT TARGET openjp2)
  message(FATAL_ERROR "OpenJPEG CMake package must export target openjp2")
endif()

add_library(ogl_openjpeg2 SHARED ogl_openjpeg2.c)
target_compile_features(ogl_openjpeg2 PRIVATE c_std_11)
target_link_libraries(ogl_openjpeg2 PRIVATE openjp2)

if(MSVC)
  # x64-windows-static vcpkg builds OpenJPEG with /MT. Match its CRT
  # to avoid LNK4098 and prevent mixed runtime allocators in the DLL.
  set_property(TARGET ogl_openjpeg2 PROPERTY
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  target_compile_options(ogl_openjpeg2 PRIVATE /W4)
else()
  target_compile_options(ogl_openjpeg2 PRIVATE -Wall -Wextra -Werror)
endif()

if(WIN32 AND CMAKE_SIZEOF_VOID_P EQUAL 8)
  set_target_properties(ogl_openjpeg2 PROPERTIES
    OUTPUT_NAME "ogl-openjpeg2-x86_64"
    PREFIX ""
  )
else()
  message(FATAL_ERROR "Use build.sh on native Linux targets; CMake packaging is currently Windows x64-only")
endif()
