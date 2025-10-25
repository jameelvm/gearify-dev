FROM node:20 AS build
WORKDIR /app
COPY gearify-web/package*.json ./
RUN npm ci
COPY gearify-web/ .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist/gearify-web/browser /usr/share/nginx/html
COPY gearify-web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
