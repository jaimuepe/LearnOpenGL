#version 450 core

struct DirLight {

  vec3 direction;

  vec3 ambient;
  vec3 diffuse;
  vec3 specular;
};

in VS_OUT {
  vec3 fragPos;
  vec3 normal;
  vec2 texCoords;
  vec4 fragPosLightSpace;
}
fs_in;

out vec4 FragColor;

uniform DirLight dirLight;

uniform sampler2D diffuse_texture0;
uniform sampler2D specular_texture0;

uniform sampler2D shadowMap;

float calculateShadow(vec3 normal) {

  // perspective division (glsl does this automatically
  // when sending gl_Position to the vertex shader)

  // now it's in the range[-1, 1]

  // this step is kinda meaningless in directional cameras since there is no
  // perspective projection but we will use it later

  vec3 projCoords = fs_in.fragPosLightSpace.xyz / fs_in.fragPosLightSpace.w;

  // depth map is in range [0, 1] so we have to adapt our coordinates to this
  // system

  projCoords = (1 + projCoords) * 0.5;

  float currentDepth = projCoords.z;

  if (currentDepth > 1.0) {
    // outside of the light view frustrum
    return 0.0;
  }

  // if the lightrays are aligned with the fragments normal we don't need a lot
  // of biasing. In the opposite case we need a higher bias to avoid shadow
  // acne.
  //  bias in range [0.001, 0.05] (this causes peter panning tho)
  float bias = max(0.001, 0.05 * (1.0 - dot(normal, -dirLight.direction)));

  // PCF (percentage-closer filtering)
  // we average 9 samples to soften the edges

  float shadow = 0.0;

  vec2 texelSize = 1.0 / textureSize(shadowMap, 0);

  for (int x = -1; x <= 1; ++x) {
    for (int y = -1; y <= 1; ++y) {

      float pcfDepth =
          texture(shadowMap, projCoords.xy + vec2(x, y) * texelSize).r;

      shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;
    }
  }

  return shadow /= 9.0;

  // without pcf

  // float closestDepth = texture(shadowMap, projCoords.xy).r;
  // return currentDepth - bias > closestDepth ? 1.0 : 0.0;
}

vec3 calculateDirLight(vec3 normal, vec3 diffuseColor, vec3 specularColor) {

  vec3 fragLightDir = -normalize(dirLight.direction);

  // ambient
  vec3 ambient = dirLight.ambient * diffuseColor;

  // diffuse

  float diff = max(0.0, dot(fragLightDir, normal));
  vec3 diffuse = diff * dirLight.diffuse * diffuseColor;

  // specular (blinn-phong)

  vec3 fragCameraDir =
      normalize(-fs_in.fragPos); // cameraPos = 0.0 in camera space

  vec3 halfwayDir = normalize(fragLightDir + fragCameraDir);
  float spec = pow(max(0.0, dot(halfwayDir, normal)), 64.0);

  vec3 specular = dirLight.specular * spec * specularColor;

  float shadow = calculateShadow(normal);

  return (ambient + (1.0 - shadow) * (diffuse + specular));
}

void main() {

  vec3 diffColor = vec3(texture(diffuse_texture0, fs_in.texCoords));
  vec3 specColor = vec3(texture(specular_texture0, fs_in.texCoords));

  vec3 n = normalize(fs_in.normal);

  vec3 result = vec3(0.0);

  result += calculateDirLight(n, diffColor, specColor);

  FragColor = vec4(result, 1.0);
}