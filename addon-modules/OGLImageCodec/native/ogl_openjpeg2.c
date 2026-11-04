/* SPDX-License-Identifier: MPL-2.0
 * OpenGenesisLINK G1.1: bounded native OpenJPEG 2.x validation shim.
 *
 * OpenJPEG's public 2.x decoder index does NOT expose packet positions.
 * This library deliberately returns metadata only. Precise layer offsets
 * must be supplied by a separate parser and checked against layer count.
 */
#include <openjpeg.h>
#include <stdint.h>
#include <string.h>

#if defined(_WIN32)
#define OGL_EXPORT __declspec(dllexport)
#else
#define OGL_EXPORT __attribute__((visibility("default")))
#endif

typedef struct {
    const uint8_t *data;
    size_t length;
    size_t cursor;
} ogl_reader;

typedef struct {
    int32_t width;
    int32_t height;
    int32_t components;
    int32_t layers;
    int32_t progression;
} ogl_j2k_info;

static OPJ_SIZE_T ogl_read(void *dst, OPJ_SIZE_T count, void *context)
{
    ogl_reader *reader = (ogl_reader *)context;
    if (!reader || !dst || reader->cursor >= reader->length)
        return (OPJ_SIZE_T)-1;
    size_t available = reader->length - reader->cursor;
    if (count > available)
        count = available;
    memcpy(dst, reader->data + reader->cursor, count);
    reader->cursor += count;
    return count;
}

static OPJ_OFF_T ogl_skip(OPJ_OFF_T offset, void *context)
{
    ogl_reader *reader = (ogl_reader *)context;
    if (!reader)
        return (OPJ_OFF_T)-1;
    if (offset < 0)
    {
        /* Avoid negating the minimum signed offset. */
        if (offset == INT64_MIN || (uint64_t)(-offset) > reader->cursor)
            return (OPJ_OFF_T)-1;
        reader->cursor -= (size_t)(-offset);
    }
    else
    {
        if ((uint64_t)offset > reader->length - reader->cursor)
            return (OPJ_OFF_T)-1;
        reader->cursor += (size_t)offset;
    }
    return offset;
}

static OPJ_BOOL ogl_seek(OPJ_OFF_T offset, void *context)
{
    ogl_reader *reader = (ogl_reader *)context;
    if (!reader || offset < 0 || (uint64_t)offset > reader->length)
        return OPJ_FALSE;
    reader->cursor = (size_t)offset;
    return OPJ_TRUE;
}

static void ogl_quiet(const char *message, void *context)
{
    (void)message;
    (void)context;
}

OGL_EXPORT const char *ogl_openjpeg2_version(void)
{
    return opj_version();
}

/* Zeroes output on every failure. No memory owning pointers cross the ABI.
 * Supports single-tile LRCP J2C/JP2 only, matching progressive layer arrays.
 * Max 32 MiB codestream, 8192x8192 dimensions, 32 Mpixels, five components.
 * A successful result means OpenJPEG decoded the full codestream; it does
 * NOT claim that quality-layer byte offsets were computed by OpenJPEG.
 */
OGL_EXPORT int ogl_openjpeg2_inspect(const uint8_t *data, int32_t length,
                                    ogl_j2k_info *output)
{
    opj_codec_t *codec = NULL;
    opj_stream_t *stream = NULL;
    opj_image_t *image = NULL;
    opj_codestream_info_v2_t *info = NULL;
    int success = 0;
    OPJ_CODEC_FORMAT format;
    ogl_reader reader;
    opj_dparameters_t params;

    if (output)
        memset(output, 0, sizeof(*output));
    if (!output || !data || length < 12 || length > 32 * 1024 * 1024)
        return 0;

    if (data[0] == 0xff && data[1] == 0x4f &&
        data[2] == 0xff && data[3] == 0x51)
        format = OPJ_CODEC_J2K;
    else if (memcmp(data, "\x00\x00\x00\x0cjP  \x0d\x0a\x87\x0a", 12) == 0)
        format = OPJ_CODEC_JP2;
    else
        return 0;

    reader.data = data;
    reader.length = (size_t)length;
    reader.cursor = 0;
    codec = opj_create_decompress(format);
    if (!codec)
        goto cleanup;
    opj_set_error_handler(codec, ogl_quiet, NULL);
    opj_set_warning_handler(codec, ogl_quiet, NULL);
    opj_set_default_decoder_parameters(&params);
    if (!opj_setup_decoder(codec, &params))
        goto cleanup;

    stream = opj_stream_create(16 * 1024, OPJ_TRUE);
    if (!stream)
        goto cleanup;
    opj_stream_set_read_function(stream, ogl_read);
    opj_stream_set_skip_function(stream, ogl_skip);
    opj_stream_set_seek_function(stream, ogl_seek);
    opj_stream_set_user_data(stream, &reader, NULL);
    opj_stream_set_user_data_length(stream, (OPJ_UINT64)length);
    if (!opj_read_header(stream, codec, &image) || !image)
        goto cleanup;

    if (image->numcomps < 1 || image->numcomps > 5 ||
        image->x1 <= image->x0 || image->y1 <= image->y0 ||
        image->x1 - image->x0 > 8192 || image->y1 - image->y0 > 8192 ||
        (uint64_t)(image->x1 - image->x0) * (image->y1 - image->y0) > 32u * 1024u * 1024u)
        goto cleanup;

    if (!opj_decode(codec, stream, image) ||
        !opj_end_decompress(codec, stream))
        goto cleanup;

    info = opj_get_cstr_info(codec);
    if (!info || info->tw != 1 || info->th != 1 ||
        info->nbcomps != image->numcomps ||
        info->m_default_tile_info.numlayers < 1 ||
        info->m_default_tile_info.numlayers > 1024 ||
        info->m_default_tile_info.prg != OPJ_LRCP)
        goto cleanup;

    output->width = (int32_t)(image->x1 - image->x0);
    output->height = (int32_t)(image->y1 - image->y0);
    output->components = (int32_t)image->numcomps;
    output->layers = (int32_t)info->m_default_tile_info.numlayers;
    output->progression = (int32_t)info->m_default_tile_info.prg;
    success = 1;

cleanup:
    if (!success)
        memset(output, 0, sizeof(*output));
    if (info)
        opj_destroy_cstr_info(&info);
    if (image)
        opj_image_destroy(image);
    if (stream)
        opj_stream_destroy(stream);
    if (codec)
        opj_destroy_codec(codec);
    return success;
}
