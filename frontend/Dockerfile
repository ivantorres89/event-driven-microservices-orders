FROM node:20-alpine

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm install

COPY . .

EXPOSE 4200

# Generate runtime config from docker-compose env vars, then start dev server.
CMD ["sh", "-c", "node scripts/generate-runtime-config.js && npm start -- --host 0.0.0.0 --poll 1000"]
